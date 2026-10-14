using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PlayerInteractor))]
public class PlayerGrabber : MonoBehaviour
{
    public Item HeldItem => heldItem;

    [SerializeField] private Transform socket;
    [SerializeField] private Transform releasePoint;

    private PlayerInteractor interactor;
    private Item heldItem;
    private int ownerActorNumber = -1;
    private bool isLocal = true;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();

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
    }

    private void LateUpdate()
    {
        if (heldItem == null)
            return;

        if (heldItem.CurrentState != Item.State.Grabbed)
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
    /// 몸 앞 지점에서 손을 놓을 때의 아이템 루트 위치·회전 (Shoot·Grab해제 시작 위치)
    /// 들고 있는 아이템이 없으면 false
    /// </summary>
    public bool TryGetReleasePose(out Vector3 pos, out Quaternion rot)
    {
        if (heldItem == null)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }

        // 몸 앞 지점에 바닥 가운데를 맞춰 배치
        GetPlacement(releasePoint.position, out pos, out rot);
        return true;
    }

    private void HandleItemGrabbed(Item grabbedItem, int holdingActorNumber)
    {
        if (holdingActorNumber != ownerActorNumber)
            return;

        heldItem = grabbedItem;
    }

    private void TryGrab()
    {
        //이미 들고 있으면  종료
        if (heldItem != null)
        {
            Debug.Log($"[PlayerGrabber] 이미 들고 있음: {heldItem.ItemData.ItemName}");
            return;
        }

        IInteractable target = interactor.CurrentTarget;
        if (target == null || !target.CanGrab())
            return;

        target.OnGrab();
    }

    /// <summary>
    /// 몸 앞 지점에서 내려놓기 요청. 해제(heldItem 비우기)는 LateUpdate가 상태를 보고 처리
    /// </summary>
    private void TryDrop()
    {
        if (!TryGetReleasePose(out Vector3 pos, out Quaternion rot))
            return;

        heldItem.Drop(pos, rot);
    }
}