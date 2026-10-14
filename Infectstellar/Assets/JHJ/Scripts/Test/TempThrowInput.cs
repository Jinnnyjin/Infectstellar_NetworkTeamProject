// [임시] 아이템 Shoot 네트워크 테스트용 던지기 입력
// 실제 던지기 입력은 플레이어 담당이 추가 예정, 통합 시 삭제 예정
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [임시] F: 들고 있는 아이템을 aimPivot 정면으로 던짐
/// 부착 위치: 플레이어 루트 (PlayerGrabber와 같은 오브젝트)
/// 따라가기 해제는 PlayerGrabber가 아이템 상태를 보고 처리
/// </summary>
public class TempThrowInput : MonoBehaviour // [임시]
{
    [Tooltip("던질 방향 기준 (PlayerInteractor의 ViewPivot과 같은 오브젝트)")]
    [SerializeField] private Transform aimPivot;

    private PlayerGrabber grabber;
    private bool isLocal = true; // PhotonView 없거나 방 밖이면 내 것


    private void Awake()
    {
        grabber = GetComponent<PlayerGrabber>();
        if (grabber == null)
        {
            Debug.LogError($"[TempThrowInput] {gameObject.name}: PlayerGrabber가 없음", this);
            enabled = false;
            return;
        }

        if (aimPivot == null)
        {
            Debug.LogError($"[TempThrowInput] {gameObject.name}: aimPivot이 연결되지 않음", this);
            enabled = false;
            return;
        }

        // [임시] 내 플레이어 판단 (PlayerGrabber와 같은 규칙)
        PhotonView view = GetComponent<PhotonView>();
        if (view != null && PhotonNetwork.InRoom)
        {
            isLocal = view.IsMine;
        }
    }

    /// <summary>
    /// [임시] 런타임에 AddComponent로 붙일 때 Awake 전에 호출 (TempPlayerNetworkSpawner 사용)
    /// </summary>
    public void SetAimPivot(Transform pivot)
    {
        aimPivot = pivot;
    }

    private void Update()
    {
        if (!isLocal)
            return;

        // [임시] 입력 액션 추가 전까지 키 직접 읽기
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
            return;

        TryThrow();
    }

    // [임시] 반드시 Item.Shoot을 거침 (RPC·힘 크기는 Item 쪽 담당)
    private void TryThrow()
    {
        Item heldItem = grabber.HeldItem;
        if (heldItem == null)
        {
            Debug.Log("[TempThrowInput] F: 들고 있는 아이템 없음", this);
            return;
        }

        if (!grabber.TryGetReleasePose(out Vector3 startPos, out Quaternion startRot))
        {
            Debug.LogWarning($"[TempThrowInput] F: 시작 위치 구하기 실패 - {heldItem.ItemData.ItemName}", this);
            return;
        }

        Vector3 direction = aimPivot.forward;
        heldItem.Shoot(startPos, startRot, direction);

        Debug.Log($"[TempThrowInput] 던지기: {heldItem.ItemData.ItemName}, 시작 {startPos:F3}, 방향 {direction:F3}", this);
    }
}
