using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PlayerInteractor))]
public class PlayerGrabber : MonoBehaviour
{
    [SerializeField] private Transform socket;

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
            TryGrab();
    }

    private void LateUpdate()
    {
        if (heldItem == null)
            return;

        if(heldItem.CurrentState != Item.State.Grabbed)
        {
            Debug.Log($"[PlayerGrabber] 들기 해제: {heldItem.ItemData.ItemName} ({heldItem.CurrentState})");
            heldItem = null;

            return;
        }

        Vector3 position = socket.position - socket.rotation * heldItem.UprightBottomCenter;
        heldItem.transform.SetPositionAndRotation(position, socket.rotation);
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

}
