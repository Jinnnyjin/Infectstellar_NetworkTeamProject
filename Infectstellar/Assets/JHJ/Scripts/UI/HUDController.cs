using ENZEUN.Runtime;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Photon.Pun;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class HUDController : MonoBehaviour
{
    [SerializeField] private HeldItemView heldItemView;
    [SerializeField] private InteractInfoView interactInfoView;
    [SerializeField] private KeyPromptView promptView;
    [SerializeField] private MMF_Player imageFader;
    [SerializeField] private MMProgressBar hp_Bar;
    [SerializeField] private MMProgressBar loading_Bar;
    [SerializeField] private CanvasGroup failedCanvas;
    [SerializeField] private CanvasGroup escapedCanvas;
    [ShowInInspector, ReadOnly] private PlayerDataObserver observer;
    private GameManager gm;

    public UnityEvent OnHpChanged;
    /// <summary>
    /// 내 플레이어 등록 후 호출
    /// </summary>
    public void InitialSetting()
    {
        gm = GameManager.Instance;

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

        gm.OnFailed += ShowFailedUI;
        gm.OnEscaped += ShowEscapeddUI;


    }

    private void OnDisable()
    {
        if (observer == null)
            return;

        observer.OnPlayerHpChanged -= ChangeHpBar;

        if (gm == null)
            return;
        gm.OnFailed -= ShowFailedUI;
        gm.OnEscaped -= ShowEscapeddUI;

    }

    private void ChangeHpBar()
    {
#if UNITY_EDITOR
        Debug.Log($"[HUD Controller] : 플레이어 체력변경 이벤트 발생!!! HP:{observer.hp}");
#endif
        hp_Bar.UpdateBar(observer.hp, 0f, 100f);
        OnHpChanged?.Invoke();
    }

    [Button]
    private void ChangeHpBarTarget(float hp)
    {
        hp_Bar.UpdateBar(hp, 0f, 100f);
        OnHpChanged?.Invoke();
    }


    [Button]
    public void FadeInDarkImage(bool fadeIn)
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

    public void ChangeHUD_LoadingBar(float value)
    {
        loading_Bar.UpdateBar(value, 0f, 100f);
    }

    private void ShowFailedUI()
    {
        failedCanvas.gameObject.SetActive(true);
    }

    private void ShowEscapeddUI()
    {
        escapedCanvas.gameObject.SetActive(true);
    }

}
