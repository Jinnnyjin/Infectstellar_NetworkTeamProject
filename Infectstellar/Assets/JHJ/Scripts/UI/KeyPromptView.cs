using TMPro;
using UnityEngine;

public class KeyPromptView : MonoBehaviour
{
    [SerializeField] private Canvas slotCanvas;
    [SerializeField] private TMP_Text promptText;

    // [임시] 입력 방식 확정 시 교체 (던지기는 우클릭 예정)
    [SerializeField] private string grabText = "[E] 잡기";
    [SerializeField] private string cannotGrabText = "잡기 불가";
    [SerializeField] private string heldText = "[E] 놓기\n[F] 던지기";

    private PlayerInteractor interactor;
    private PlayerGrabber grabber;
    private bool lastCanGrab;


    private void Awake()
    {
        slotCanvas.enabled = false;
    }

    public void InitialSetting(PlayerInteractor newInteractor, PlayerGrabber newGrabber)
    {
        if (newInteractor == null || newGrabber == null)
        {
            Debug.LogError("[KeyPromptView] InitialSetting 실패: interactor 또는 grabber가 null", this);
            return;
        }

        Unsubscribe();

        interactor = newInteractor;
        grabber = newGrabber;

        interactor.targetChanged += HandleTargetChanged;
        grabber.HeldItemChanged += HandleHeldItemChanged;

        Refresh();
    }

    private void Update()
    {
        if (interactor == null || grabber == null)
            return;
        if (grabber.HeldItem != null)
            return;

        IInteractable target = interactor.CurrentTarget;
        if (target == null)
            return;

        if (target.CanGrab() != lastCanGrab)
            Refresh();
    }


    private void HandleTargetChanged(IInteractable interactable)
    {
        Refresh();
    }

    private void HandleHeldItemChanged(Item item)
    {
        Refresh();
    }

    private void Refresh()
    {
        if(grabber.HeldItem != null)
        {
            promptText.text = heldText;
            slotCanvas.enabled = true;
            return;
        }

        IInteractable target = interactor.CurrentTarget;
        if(target == null)
        {
            slotCanvas.enabled = false;
            return;
        }

        lastCanGrab = target.CanGrab();
        promptText.text = lastCanGrab ? grabText : cannotGrabText;
        slotCanvas.enabled = true;
    }


    private void Unsubscribe()
    {
        if (interactor != null)
            interactor.targetChanged -= HandleTargetChanged;

        if (grabber != null)
            grabber.HeldItemChanged -= HandleHeldItemChanged;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
