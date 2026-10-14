using Photon.Pun;
using System;
using UnityEngine;

[RequireComponent(typeof(PlayerInteractor))]
public class PlayerGrabber : MonoBehaviour
{
    public Item HeldItem => heldItem;
    /// <summary>초기 동기화에 현재 소지한 아이템만 전달할 네트워크 식별자입니다.</summary>
    public int HeldItemViewId => heldItem != null && heldItem.CurrentState == Item.State.Grabbed
        && heldItem.HoldingActorNumber == ownerActorNumber ? GetItemViewId(heldItem) : 0;

    [SerializeField] private Transform socket;

    [Tooltip("ViewPivot 자식. 로컬 회전 0 유지 (정면 = 던지는 방향)")]
    [SerializeField] private Transform releasePoint;

    private PlayerInteractor interactor;
    private Item heldItem;
    private int ownerActorNumber = -1;
    private bool isLocal = true;
    private _PlayerNetworkSync networkSync;
    private PlayerStateMachine stateMachine;

    public event Action<bool> GrabRequested;
    public event Action<bool> DropRequested;
    public event Action<bool> ShootRequested;
    #region 심지태 수정
    public event Action<Item> D_Grabbed;
    public event Action<Item, Vector3, Quaternion> D_Dropped;
    public event Action<Item, Vector3, Quaternion, Vector3> D_Shot;
    #endregion

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        networkSync = GetComponent<_PlayerNetworkSync>();
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
        if (!isLocal || (networkSync != null && !networkSync.IsLocalControlActive))
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

        if (heldItem.CurrentState != Item.State.Grabbed || heldItem.HoldingActorNumber != ownerActorNumber)
        {
            Debug.Log($"[PlayerGrabber] 들기 해제: {heldItem.ItemData.ItemName} ({heldItem.CurrentState})");
            heldItem = null;

            return;
        }

        GetPlacement(socket.position, out Vector3 position, out Quaternion rotation);
        heldItem.transform.SetPositionAndRotation(position, rotation);
    }

    // =============================================================
    // 메서드

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

        heldItem = grabbedItem;
    }

    private bool CanHandleRequest(string requestName)
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning($"[PlayerGrabber] {requestName} 무시: 컴포넌트 꺼짐", this);
            return false;
        }

        if (!isLocal)
        {
            Debug.LogWarning($"[PlayerGrabber] {requestName} 무시: 내 플레이어 아님", this);
            return false;
        }
        if (networkSync != null && !networkSync.IsLocalControlActive)
            return false;

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

        Item item = target as Item;
        if (item == null || (networkSync != null && GetItemViewId(item) <= 0) || !ApplyGrab(item))
            return false;

        #region 심지태
        D_Grabbed?.Invoke(item);
        #endregion
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

        Item item = heldItem;
        if (networkSync == null)
            item.Drop(pos, rot);
        else if (!ApplyDrop(item, pos, rot))
            return false;

        #region 심지태
        D_Dropped?.Invoke(item, pos, rot);
        #endregion
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
        Item item = heldItem;
        Vector3 force = item.CalculateThrowForce(direction);
        if (networkSync == null)
            item.Shoot(pos, rot, direction);
        else if (!ApplyShoot(item, pos, rot, force))
            return false;

        #region 심지태
        D_Shot?.Invoke(item, pos, rot, force);
        #endregion
        ShootRequested?.Invoke(false);

        Debug.Log($"[PlayerGrabber] Shoot 요청: {item.ItemData.ItemName}, 시작 {pos:F3}, 방향 {direction:F3}", this);
        return true;
    }

    /// <summary>RPC가 지정한 아이템을 잡으며 로컬 요청·송신 이벤트는 발생시키지 않습니다.</summary>
    public bool TryGrab(int itemViewId) => ApplyGrab(FindItem(itemViewId));

    /// <summary>RPC의 확정된 시작 위치·회전으로 내려놓고 재송신하지 않습니다.</summary>
    public bool TryDrop(int itemViewId, Vector3 position, Quaternion rotation) => ApplyDrop(FindItem(itemViewId), position, rotation);

    /// <summary>RPC의 확정된 시작 위치·회전·힘으로 발사하고 재송신하지 않습니다.</summary>
    public bool TryShoot(int itemViewId, Vector3 position, Quaternion rotation, Vector3 force) => ApplyShoot(FindItem(itemViewId), position, rotation, force);

    /// <summary>소지자 번호를 사용해 같은 아이템의 상태와 소켓 추적을 연결합니다.</summary>
    private bool ApplyGrab(Item item)
    {
        if (!isActiveAndEnabled || item == null || heldItem != null || !item.ApplyGrab(ownerActorNumber))
            return false;
        heldItem = item;
        return true;
    }

    /// <summary>현재 소지한 아이템의 내려놓기 상태·물리를 적용합니다.</summary>
    private bool ApplyDrop(Item item, Vector3 position, Quaternion rotation)
    {
        if (!isActiveAndEnabled || item == null || heldItem != item
            || !item.ApplyPlayerDrop(position, rotation, ownerActorNumber, isLocal))
            return false;
        heldItem = null;
        return true;
    }

    /// <summary>현재 소지한 아이템의 발사 상태·물리와 판정 주체를 적용합니다.</summary>
    private bool ApplyShoot(Item item, Vector3 position, Quaternion rotation, Vector3 force)
    {
        if (!isActiveAndEnabled || item == null || heldItem != item
            || !item.ApplyPlayerShoot(position, rotation, force, ownerActorNumber, isLocal))
            return false;
        heldItem = null;
        return true;
    }

    /// <summary>모든 클라이언트가 같은 아이템을 찾을 네트워크 식별자를 읽습니다.</summary>
    private static int GetItemViewId(Item item)
    {
        PhotonView itemView = item != null ? item.GetComponent<PhotonView>() : null;
        return itemView != null ? itemView.ViewID : 0;
    }

    /// <summary>원격 레이캐스트 없이 RPC에 지정된 아이템을 찾습니다.</summary>
    private static Item FindItem(int itemViewId)
    {
        PhotonView itemView = PhotonView.Find(itemViewId);
        return itemView != null ? itemView.GetComponent<Item>() : null;
    }
}
