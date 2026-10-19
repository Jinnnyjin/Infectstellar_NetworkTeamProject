// [임시] HUD 동작 확인용 로컬 플레이어 연결
// 게임 매니저가 플레이어 생성 직후 HUDController.InitialSetting 호출로 대체 예정, 통합 시 삭제
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [임시] 연결 키(기본 F1): 씬에서 IsMine + PlayerGrabber 오브젝트를 찾아 HUDController.InitialSetting 호출
/// 키 입력 방식: 네트워크 생성 타이밍과 무관하게 테스트하기 위함
/// 부착 위치: 씬 아무 오브젝트 (HUD 쪽 권장)
/// </summary>
public class HUDTestBinder : MonoBehaviour // [임시]
{
    [SerializeField] private HUDController hudController;
    [SerializeField] private Key bindKey = Key.F1;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[bindKey].wasPressedThisFrame)
            return;

        Bind();
    }

    private void Bind()
    {
        if (hudController == null)
        {
            Debug.LogError("[HUDTestBinder] hudController 미연결", this);
            return;
        }

        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer == null)
        {
            Debug.LogWarning("[HUDTestBinder] IsMine이고 PlayerGrabber를 가진 오브젝트를 찾지 못함", this);
            return;
        }

        hudController.InitialSetting(localPlayer);
        Debug.Log($"[HUDTestBinder] {localPlayer.name} → HUD 연결", this);
    }

    private GameObject FindLocalPlayer()
    {
        PhotonView[] views = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        foreach (PhotonView view in views)
        {
            if (!view.IsMine)
                continue;

            if (view.GetComponent<PlayerGrabber>() != null)
                return view.gameObject;
        }
        return null;
    }
}
