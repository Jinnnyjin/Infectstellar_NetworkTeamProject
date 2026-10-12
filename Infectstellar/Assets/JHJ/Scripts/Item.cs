using UnityEngine;

[RequireComponent(typeof(ItemPhysics), typeof(ItemHit), typeof(ItemNetwork))]
public class Item : MonoBehaviour, IInteractable
{
    public enum State { None, Placed, Grabbed, Thrown }
    public State CurrentState { get; private set; }
    public ItemDataSO ItemData => data;

    [SerializeField] private ItemDataSO data;
    [SerializeField] private float throwForce; // [임시]

    private float uprightBottomOffset;
    private ItemPhysics physics;
    private ItemHit hit;
    private ItemNetwork network;
    private float stopTimer; // 누적 시간 재는 용
    private float requiredStopDuration = 0.2f; // 기준
    private int shooterActorNumber;
    private bool isLocalThrower;

    private float shootStartTime; // [임시] 측정용: 출발 시각, 정지 시 비행 시간 계산


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

        // 최하단 오프셋 계산
        if (!ColliderBoundsUtil.TryGetUprightBottomOffset(transform, physics.Colliders, out uprightBottomOffset))
        {
            Debug.LogError($"{gameObject.name} 최하단 오프셋 구하기 실패", this);
            gameObject.SetActive(false);
            return;
        }

        // 네트워크
        network = GetComponent<ItemNetwork>();
        network.Init(ExecuteShoot, ApplyStopSync);

        // ItemHit
        hit = GetComponent<ItemHit>();
        hit.Init(network.GetOwnerActorNumber);

        ChangeState(State.Placed);
    }

    private void FixedUpdate()
    {
        if (CurrentState != State.Thrown)
            return;

        if (physics.IsBelowStopThreshold())
        {
            stopTimer += Time.fixedDeltaTime;
            if (stopTimer > requiredStopDuration)
            {
                ChangeState(State.Placed);
                // 판정 컴퓨터만 최종 위치를 나머지에게 보냄 (자신은 이미 그 위치)
                if (isLocalThrower)
                {
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

            case State.Thrown:
                physics.SetColliders(true); 
                physics.SetPhysics(true);
                hit.Activate(data.Attack, shooterActorNumber,isLocalThrower);
                stopTimer = 0;
                break;

            default:
                Debug.LogError($"[Item] {data.ItemName} 처리하지 않는 상태 {newState}", this);
                break;
        }

        Debug.Log($"[Item] {data.ItemName}: {prevState} → {newState}", this);
    }

    // =============================================================
    // Shoot
    public void Shoot(Vector3 direction)
    {
        if (CurrentState != State.Grabbed)
        {
            Debug.LogWarning($"[Item] Shoot 불가 : {data.ItemName} 상태 = {CurrentState}",this);
            return;
        }

        // 시작위치 : 들려 있는 동안 소켓위치 = 자기 위치 = 시작 위치
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 force = direction.normalized * throwForce;

        network.SendShoot(startPos,startRot,force);
    }

    /// <summary>
    /// ItemNetwork가 RPC 수신 시에만 호출 (Init으로 전달), 직접 호출 금지
    /// </summary>
    private void ExecuteShoot(Vector3 startPos, Quaternion startRot, Vector3 force, int shooterActorNumber, bool isLocalThrower)
    {
        this.shooterActorNumber = shooterActorNumber;
        this.isLocalThrower = isLocalThrower;

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

        if (CurrentState == State.Thrown)
            ChangeState(State.Placed);


        float correction = Vector3.Distance(transform.position, pos);
        Debug.Log($"[Item] {data.ItemName} 정지 보정: 이동 거리 {correction:F3}m", this);

        transform.SetPositionAndRotation(pos, rot);
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

        ChangeState(State.Grabbed);
    }
}
