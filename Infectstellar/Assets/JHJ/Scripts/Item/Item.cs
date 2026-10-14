using System;
using UnityEngine;

[RequireComponent(typeof(ItemPhysics), typeof(ItemHit), typeof(ItemNetwork))]
public class Item : MonoBehaviour, IInteractable
{
    public enum State { None, Placed, Grabbed, Dropped, Thrown }
    public State CurrentState { get; private set; }
    /// <summary>모든 복제본에서 동일하게 기록할 현재 소지자의 ActorNumber입니다.</summary>
    public int HoldingActorNumber { get; private set; } = -1;
    public ItemDataSO ItemData => data;
    /// <summary>
    /// 똑바로 선 기준 루트 → 콜라이더 바닥면 가운데 지점 (루트 로컬 위치). 소켓 배치용
    /// </summary>
    public Vector3 UprightBottomCenter => uprightBottomCenter;
    /// <summary>
    /// 잡혔을때 발생, 각 플레이어가 자기 번호와 같으면 들기
    /// </summary>
    public static event Action<Item, int> Grabbed;

    [SerializeField] private ItemDataSO data;
    [SerializeField] private float throwForce; // [임시]

    private Vector3 uprightBottomCenter;
    private ItemPhysics physics;
    private ItemHit hit;
    private ItemNetwork network;
    private float stopTimer; // 누적 시간 재는 용
    private float requiredStopDuration = 0.2f; // 기준
    private int shooterActorNumber;
    private bool isLocalReleaser;
    private float shootStartTime; // [임시] 측정용: 출발 시각, 정지 시 비행 시간 계산
    private PlayerNetworkSync playerReleaseSource; // 최종 정지를 플레이어 재생 시계로 보낼 연결점
    private int playerReleaseSequence; // 최종 정지가 속한 내려놓기·발사 순서
    private bool hasPlayerActionContext; // 이미 처리한 동작보다 과거의 정지 보정을 제외할 기준


    private void Awake()
    {
        if(data == null)
        {
            Debug.LogError($"{gameObject.name} ItemDataSO가 연결되지 않음", this);
            gameObject.SetActive(false);
            return;
        }

        // 아이템 물리 Init
        physics = GetComponent<ItemPhysics>();
        if(!physics.Init(data.Weight))
        {
            Debug.LogError($"{gameObject.name} 켜진 콜라이더가 없음", this);
            gameObject.SetActive(false);
            return;
        }

        // 바닥 중심 계산 (피벗이 모델마다 달라 소켓 배치 보정용)
        if (!ColliderBoundsUtil.TryGetUprightBottomCenter(transform, physics.Colliders, out uprightBottomCenter))
        {
            Debug.LogError($"{gameObject.name} 바닥 중심 구하기 실패", this);
            gameObject.SetActive(false);
            return;
        }

        // 네트워크
        network = GetComponent<ItemNetwork>();
        network.Init(ExecuteShoot, ApplyStopSync, ExecuteDrop);

        // ItemHit
        hit = GetComponent<ItemHit>();
        hit.Init(network.GetOwnerActorNumber, data);

        ChangeState(State.Placed);
    }

    private void FixedUpdate()
    {
        if (CurrentState != State.Thrown && CurrentState != State.Dropped)
            return;

        if (physics.IsBelowStopThreshold())
        {
            stopTimer += Time.fixedDeltaTime;
            if (stopTimer > requiredStopDuration)
            {
                ChangeState(State.Placed);
                // 판정 컴퓨터만 최종 위치를 나머지에게 보냄 (자신은 이미 그 위치)
                if (isLocalReleaser)
                {
                    if (playerReleaseSource != null)
                        playerReleaseSource.SendItemStop(this, playerReleaseSequence, transform.position, transform.rotation);
                    else
                        network.SendStop(transform.position, transform.rotation);
                }
            }
        }
        else
        {
            stopTimer = 0f;
        }
    }

    // =============================================================
    // 상태 전환
    private void ChangeState(State newState)
    {
        if(newState == CurrentState)
        {
            Debug.LogWarning($"[Item] {data.ItemName} 이미 {CurrentState} 상태, 전환 무시", this);
            return;
        }

        State prevState = CurrentState;
        CurrentState = newState;

        switch(newState)
        {
            case State.Placed:
                physics.SetPhysics(false);
                physics.SetColliders(true); // 레이캐스트 대상
                hit.Deactivate(); // 히트박스 꺼짐
                Debug.Log($"[Item] {data.ItemName} 정지: 비행 {Time.time - shootStartTime:F2}초, 위치 {transform.position:F3}, 회전 {transform.eulerAngles:F1}", this);
                break;

            case State.Grabbed:
                physics.SetPhysics(false);
                physics.SetColliders(false); // 몸, 벽이랑 부딪히지 않게
                hit.Deactivate();
                break;

            case State.Dropped:
                physics.SetColliders(true);
                physics.SetPhysics(true);
                hit.Deactivate();
                stopTimer = 0f;
                break;

            case State.Thrown:
                physics.SetColliders(true); 
                physics.SetPhysics(true);
                hit.Activate(data.Attack, shooterActorNumber,isLocalReleaser);
                stopTimer = 0f;
                break;

            default:
                Debug.LogError($"[Item] {data.ItemName} 처리하지 않는 상태 {newState}", this);
                break;
        }

        Debug.Log($"[Item] {data.ItemName}: {prevState} → {newState}", this);
    }

    // =============================================================
    // Shoot
    public void Shoot(Vector3 startPos, Quaternion startRot, Vector3 direction)
    {
        if (CurrentState != State.Grabbed)
        {
            Debug.LogWarning($"[Item] Shoot 불가 : {data.ItemName} 상태 = {CurrentState}",this);
            return;
        }

        Vector3 force = CalculateThrowForce(direction);

        network.SendShoot(startPos,startRot,force);
    }

    /// <summary>
    /// ItemNetwork가 RPC 수신 시에만 호출 (Init으로 전달), 직접 호출 금지
    /// </summary>
    private void ExecuteShoot(Vector3 startPos, Quaternion startRot, Vector3 force, int shooterActorNumber, bool isLocalReleaser)
    {
        playerReleaseSource = null;
        playerReleaseSequence = 0;
        HoldingActorNumber = -1;
        this.shooterActorNumber = shooterActorNumber;
        this.isLocalReleaser = isLocalReleaser;

        // 던진 사람 컴퓨터에서는 이미 같은 값. 다른 컴퓨터는 지연된 위치에 있으므로 받은 시작 위치로 맞춤
        transform.SetPositionAndRotation(startPos, startRot);

        // Thrown으로 상태 바꿔야 물리 켜짐. 순서 : 상태전환 -> Launch
        ChangeState(State.Thrown);

        Debug.Log($"[Item] {data.ItemName} Shoot: 던진 사람 {shooterActorNumber}, 시작 {startPos}, 시작 회전 {transform.eulerAngles:F1}, 힘 {force}, 무게 {data.Weight}", this);
        physics.Launch(force);

        // 출발 시각 기록 ([임시] 측정용)
        shootStartTime = Time.time;
    }

    // =============================================================
    // Stop
    /// <summary>
    /// ItemNetwork가 RPC 수신 시에만 호출 (Init으로 전달), 직접 호출 금지
    /// </summary>
    private void ApplyStopSync(Vector3 pos, Quaternion rot)
    {
        if (CurrentState == State.Grabbed)
        {
            Debug.Log($"[Item] {data.ItemName} 정지 보정 무시: 이미 누가 들고 있음", this);
            return;
        }

        if (CurrentState == State.Thrown || CurrentState == State.Dropped)
            ChangeState(State.Placed);


        float correction = Vector3.Distance(transform.position, pos);
        Debug.Log($"[Item] {data.ItemName} 정지 보정: 이동 거리 {correction:F3}m", this);

        transform.SetPositionAndRotation(pos, rot);
    }

    // =============================================================
    // Drop
    public void Drop(Vector3 startPos, Quaternion startRot)
    {
        if (CurrentState != State.Grabbed)
        {
            Debug.LogWarning($"[Item] Drop 불가 : {data.ItemName} 상태 = {CurrentState}", this);
            return;
        }

        network.SendDrop(startPos, startRot);
    }

    /// <summary>
    /// ItemNetwork가 RPC 수신 시에만 호출 (Init으로 전달), 직접 호출 금지
    /// </summary>
    private void ExecuteDrop(Vector3 startPos, Quaternion startRot, bool isLocalReleaser)
    {
        playerReleaseSource = null;
        playerReleaseSequence = 0;
        HoldingActorNumber = -1;
        this.isLocalReleaser = isLocalReleaser;
        transform.SetPositionAndRotation(startPos, startRot);
        ChangeState(State.Dropped);
    }

    // =============================================================
    // 인터페이스

    public InteractableInfo GetInfo()
    {
        return new InteractableInfo(data.ItemName, data.Price);
    }

    public bool CanGrab()
    {
        return CurrentState == State.Placed;
    }

    public void OnGrab()
    {
        if (!CanGrab())
        {
            Debug.LogWarning($"[Item] OnGrab 불가 :{data.ItemName}, 상태 {CurrentState}", this);
            return;
        }

        ApplyGrab(network.GetLocalActorNumber());
    }

    /// <summary>기존 Inspector 발사 힘으로 방향을 확정된 충격량으로 바꿉니다.</summary>
    public Vector3 CalculateThrowForce(Vector3 direction) => direction.normalized * throwForce;

    /// <summary>지정 소지자의 잡기 상태를 적용하며 RPC를 송신하지 않습니다.</summary>
    public bool ApplyGrab(int holderActorNumber)
    {
        if (!isActiveAndEnabled || physics == null || (CurrentState != State.Placed
            && !(CurrentState == State.Grabbed && HoldingActorNumber == holderActorNumber)))
            return false;
        if (CurrentState == State.Grabbed)
            return true;

        playerReleaseSource = null;
        playerReleaseSequence = 0;
        HoldingActorNumber = holderActorNumber;
        hasPlayerActionContext = true;
        ChangeState(State.Grabbed);
        Grabbed?.Invoke(this, holderActorNumber);
        return true;
    }

    /// <summary>소지자가 확정한 내려놓기를 기존 ItemNetwork RPC 없이 적용합니다.</summary>
    public bool ApplyPlayerDrop(Vector3 position, Quaternion rotation, int holderActorNumber, bool localReleaser)
    {
        if (!isActiveAndEnabled || CurrentState != State.Grabbed || HoldingActorNumber != holderActorNumber)
            return false;
        ExecuteDrop(position, rotation, localReleaser);
        return true;
    }

    /// <summary>소지자가 확정한 발사를 적용하고 발사 클라이언트에만 적중 판정을 허용합니다.</summary>
    public bool ApplyPlayerShoot(Vector3 position, Quaternion rotation, Vector3 force, int holderActorNumber, bool localReleaser)
    {
        if (!isActiveAndEnabled || CurrentState != State.Grabbed || HoldingActorNumber != holderActorNumber)
            return false;
        ExecuteShoot(position, rotation, force, holderActorNumber, localReleaser);
        return true;
    }

    /// <summary>최종 정지가 현재 내려놓기·발사와 연결되도록 플레이어와 순서를 보관합니다.</summary>
    public void SetPlayerReleaseContext(PlayerNetworkSync source, int releaseSequence)
    {
        playerReleaseSource = source;
        playerReleaseSequence = releaseSequence;
        hasPlayerActionContext = true;
    }

    /// <summary>새 잡기·발사에 과거 정지를 적용하지 않고 현재 해제의 최종 자세만 보정합니다.</summary>
    public void ApplyPlayerStop(PlayerNetworkSync source, int releaseSequence, Vector3 position, Quaternion rotation)
    {
        if (!isActiveAndEnabled || physics == null || CurrentState == State.Grabbed)
            return;
        // 발사 도중 입장해 아직 동작 이력이 없는 복제본도 마지막 위치를 맞춘다.
        if (!hasPlayerActionContext)
            SetPlayerReleaseContext(source, releaseSequence);
        if (playerReleaseSource != source || playerReleaseSequence != releaseSequence)
            return;
        ApplyStopSync(position, rotation);
    }
}
