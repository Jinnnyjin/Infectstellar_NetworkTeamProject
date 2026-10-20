using ENZEUN.Runtime;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Photon.Pun;
using Sirenix.OdinInspector;
using UnityEngine;

public class HUDController : MonoBehaviour
{
    [SerializeField] private HeldItemView heldItemView;
    [SerializeField] private InteractInfoView interactInfoView;
    [SerializeField] private KeyPromptView promptView;
    [SerializeField] private MMF_Player imageFader;
    [SerializeField] private MMProgressBar Hp_Bar;
    [ShowInInspector] private PlayerDataObserver observer;

    /// <summary>
    /// 내 플레이어 등록 후 호출
    /// </summary>
    public void InitialSetting()
    {
        GameManager gm = GameManager.Instance;

        if (gm == null)
        {
            Debug.LogError("[HUDController] GameManager 없음", this);
            return;
        }

        int myActorNum = PhotonNetwork.LocalPlayer.ActorNumber;

        if (!gm.playerDataDict.TryGetValue(myActorNum, out observer) || observer == null)
        {
            Debug.LogError($"[HUDController] 내 플레이어 데이터가 등록되지 않음 (ActorNr {myActorNum})", this);
            return;
        }

        observer.OnPlayerHpChanged += ChangeHpBar;

        GameObject player = observer.gameObject;

        PlayerGrabber grabber = player.GetComponent<PlayerGrabber>();
        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();

        if (grabber == null)
            Debug.LogError($"[HUDController] {player.name}의 PlayerGrabber 없음", this);
        if (interactor == null)
            Debug.LogError($"[HUDController] {player.name}의 PlayerInteractor 없음", this);

        // 들고있는 아이템 HUD
        if (heldItemView == null)
            Debug.LogError("[HUDController] heldItemView 미연결", this);
        else if (grabber != null)
            heldItemView.InitialSetting(grabber);

        // 상호작용HUD
        if (interactInfoView == null)
            Debug.LogError("[HUDController] interactInfoView 미연결", this);
        else if (grabber != null && interactor != null)
            interactInfoView.InitialSetting(interactor, grabber);

        // 키 프롬프트 HUD
        if (promptView == null)
            Debug.LogError("[HUDController] promptView 미연결", this);
        else if (grabber != null && interactor != null)
            promptView.InitialSetting(interactor, grabber);

        FadeDarkImage(false);
    }

    private void ChangeHpBar()
    {
        Hp_Bar.UpdateBar(observer.hp, 0f, 100f);
    }

    [Button]
    private void ChangeHpBarTarget(float hp)
    {
        Hp_Bar.UpdateBar(hp, 0f, 100f);
    }


    [Button]
    private void FadeDarkImage(bool fadeIn)
    {
        if (fadeIn)
        {
            imageFader.PlayFeedbacksTopToBottom();
        }
        else
        {
            imageFader.PlayFeedbacksBottomToTop();

        }
    }
}
