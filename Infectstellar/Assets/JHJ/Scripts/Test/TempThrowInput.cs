// [임시] 아이템 Shoot 테스트용 던지기 입력
// 실제 던지기 입력은 플레이어 담당이 TryShoot 호출로 연결 예정, 통합 시 삭제
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [임시] F: PlayerGrabber.TryShoot 호출
/// 부착 위치: 플레이어 루트 (PlayerGrabber와 같은 오브젝트)
/// </summary>
public class TempThrowInput : MonoBehaviour // [임시]
{
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

        // [임시] 한 창에 남의 플레이어 복사본도 있어 F가 같이 눌리므로 내 플레이어만 처리
        PhotonView view = GetComponent<PhotonView>();
        if (view != null && PhotonNetwork.InRoom)
            isLocal = view.IsMine;
    }

    private void Update()
    {
        if (!isLocal)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
            return;

        grabber.TryShoot();
    }
}