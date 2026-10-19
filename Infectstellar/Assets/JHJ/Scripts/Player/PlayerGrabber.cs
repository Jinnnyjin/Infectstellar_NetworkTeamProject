using Photon.Pun;
using System;
using UnityEngine;

[RequireComponent(typeof(PlayerInteractor))]
public class PlayerGrabber : MonoBehaviour
{
    public Item HeldItem => heldItem;

    [SerializeField] private Transform socket;

    [Tooltip("ViewPivot 자식. 로컬 회전 0 유지 (정면 = 던지는 방향)")]
    [SerializeField] private Transform releasePoint;

    private PlayerInteractor interactor;
    private Item heldItem;
    private int ownerActorNumber = -1;
    private bool isLocal = true;
    private PlayerStateMachine stateMachine;

    public event Action<bool> GrabRequested;
    public event Action<bool> DropRequested;
    public event Action<bool> ShootRequested;

    /// <summary>
    /// 들고 있는 아이템 바뀔때(잡은 아이템, 해제 시 null)
    /// 모든 컴퓨터에서 발생하기 때문에 HUD는 자신의 플레이어 것만 구독
    /// </summary>
    public event Action<Item> HeldItemChanged;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        stateMachine = GetComponent<PlayerStateMachine>();

        // 액터번호, 내 플레이어 여부 : PhotonView 없거나 방 밖이면 기본값(-1, 내 것) 유지
        PhotonView view = GetComponent<PhotonView>();
        if (view != null && PhotonNetwork.InRoom)
        {
            ownerActorNumber = view.OwnerActorNr;
            isLocal = view.IsMine;
        }

        if (socket == null)
        {
            Debug.LogError($"[PlayerGrabber] {gameObject.name}: 소켓이 연결되지 않음", this);
            enabled = false;
            return;
        }

        if (releasePoint == null)
        {
            Debug.LogError($"[PlayerGrabber] {gameObject.name}: releasePoint가 연결되지 않음", this);
            enabled = false;
            return;
        }
        // 추후 네트워크 작업할 때도 타 플레이어 끄면 안됨 (그사람이 들고있는 아이템 옮기기)
    }

    private void OnEnable()
    {
        Item.Grabbed += HandleItemGrabbed;
    }

    private void OnDisable()
    {
        Item.Grabbed -= HandleItemGrabbed;
    }

    private void Update()
    {
        if (!isLocal)
            return;

        // [임시] 입력 액션(Interact) 추가 전까지 키 직접 읽기
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldItem != null)
                TryDrop();
            else
                TryGrab();
        }

        if (Input.GetKeyDown(KeyCode.F))
            TryShoot();
    }

    private void LateUpdate()
    {
        if (heldItem == null)
            return;

        if (heldItem.CurrentState != Item.State.Grabbed)
        {
            Debug.Log($"[PlayerGrabber] 들기 해제: {heldItem.ItemData.ItemName} ({heldItem.CurrentState})");
            SetHeldItem(null);

            return;
        }

        GetPlacement(socket.position, out Vector3 position, out Quaternion rotation);
        heldItem.transform.SetPositionAndRotation(position, rotation);
    }

    // =============================================================
    // 메서드

    /// <summary>
    /// heldItem 변경하는 메서드
    /// 값이 바뀔때만 HeldItemChanged
    /// </summary>
    private void SetHeldItem(Item item)
    {
        if (heldItem == null)
            return;

        heldItem = item;
        HeldItemChanged?.Invoke(heldItem);
    }

    /// <summary>
    /// 기준 지점에 들고 있는 아이템의 바닥 가운데를 맞췄을 때의 루트 위치·회전 (회전은 몸 기준, 좌우만)
    /// heldItem이 있을 때만 호출
    /// </summary>
    private void GetPlacement(Vector3 anchorPos, out Vector3 pos, out Quaternion rot)
    {
        rot = transform.rotation;
        pos = anchorPos - rot * heldItem.UprightBottomCenter;
    }

    /// <summary>
    /// 시선 앞 지점(releasePoint)에서 손을 놓을 때의 아이템 루트 위치·회전 (Shoot·Drop 시작 위치)
    /// 회전은 몸 기준 좌우만 (아이템은 똑바로 선 채 출발)
    /// 들고 있는 아이템이 없으면 false
    /// </summary>
    private bool TryGetReleasePose(out Vector3 pos, out Quaternion rot)
    {
        if (heldItem == null)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }

        // 시선 앞 지점에 바닥 가운데를 맞춰 배치
        GetPlacement(releasePoint.position, out pos, out rot);
        return true;
    }

    private void HandleItemGrabbed(Item grabbedItem, int holdingActorNumber)
    {
        if (holdingActorNumber != ownerActorNumber)
            return;

        SetHeldItem(grabbedItem);
    }

    private bool CanHandleRequest(string requestName)
    {
        if(!isActiveAndEnabled)
        {
            Debug.LogWarning($"[PlayerGrabber] {requestName} 무시: 컴포넌트 꺼짐", this);
            return false;
        }

        if(!isLocal)
        {
            Debug.LogWarning($"[PlayerGrabber] {requestName} 무시: 내 플레이어 아님", this);
            return false;
        }

        return true;
    }

    // =============================================================

    /// <summary>
    /// 바라보는 대상 잡기 요청
    /// true = 요청을 보냄. HeldItem은 Item.Grabbed 알림을 받은 뒤 채워짐 (현재는 로컬이라 즉시)
    /// </summary>
    public bool TryGrab()
    {
        if (!CanHandleRequest(nameof(TryGrab)))
            return false;
        if (stateMachine == null || !stateMachine.IsAlive)
            return false;

        //이미 들고 있으면  종료
        if (heldItem != null)
        {
            Debug.Log($"[PlayerGrabber] 이미 들고 있음: {heldItem.ItemData.ItemName}");
            return false;
        }

        IInteractable target = interactor.CurrentTarget;
        if (target == null || !target.CanGrab())
            return false;

        target.OnGrab();
        GrabRequested?.Invoke(true);
        return true;
    }

    /// <summary>
    /// 시선 앞 지점에서 내려놓기 요청
    /// true = 요청을 보냄 (성공 보장 아님). 해제(heldItem 비우기)는 LateUpdate가 상태를 보고 처리
    /// </summary>
    public bool TryDrop()
    {
        if (!CanHandleRequest(nameof(TryDrop)))
            return false;

        if (!TryGetReleasePose(out Vector3 pos, out Quaternion rot))
            return false;

        heldItem.Drop(pos, rot);
        DropRequested?.Invoke(false);
        return true;
    }

    /// <summary>
    /// 시선 앞 지점에서 releasePoint 정면으로 던지기 요청
    /// true = 요청을 보냄 (성공 보장 아님). 해제는 LateUpdate가 상태를 보고 처리
    /// </summary>
    public bool TryShoot()
    {
        if (!CanHandleRequest(nameof(TryShoot)))
            return false;
        if (stateMachine == null || !stateMachine.IsAlive)
            return false;

        if (heldItem == null)
        {
            Debug.LogWarning("[PlayerGrabber] TryShoot 무시: 들고 있는 아이템 없음", this);
            return false;
        }

        if (!TryGetReleasePose(out Vector3 pos, out Quaternion rot))
        {
            Debug.LogWarning($"[PlayerGrabber] TryShoot: 시작 위치 구하기 실패 - {heldItem.ItemData.ItemName}", this);
            return false;
        }

        Vector3 direction = releasePoint.forward;
        heldItem.Shoot(pos, rot, direction);
        ShootRequested?.Invoke(false);

        Debug.Log($"[PlayerGrabber] Shoot 요청: {heldItem.ItemData.ItemName}, 시작 {pos:F3}, 방향 {direction:F3}", this);
        return true;
    }
}