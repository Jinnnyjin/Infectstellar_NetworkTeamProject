using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

public class KeyPromptView : MonoBehaviour
{
    [SerializeField] private TMP_Text promptText;

    [SerializeField] private MMF_Player visibleFb;
    private bool isVisible;

    private PlayerInteractor interactor;
    private PlayerGrabber grabber;
    private bool lastCanGrab;


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
        interactor.objectChanged += HandleObjectChanged;
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

    private void HandleObjectChanged(IInteractObject obj)
    {
        Refresh();
    }

    private void Refresh()
    {
        if(grabber.HeldItem != null)
        {
            promptText.text = InteractPromptSO.Instance.Held;
            SetVisible(true);
            return;
        }

        IInteractable target = interactor.CurrentTarget;

        if (target == null && interactor.CurrentObject != null)
        {
            promptText.text = interactor.CurrentObject.PromptText;
            SetVisible(true);
            return;
        }

        if (target == null)
        {
            SetVisible(false);
            return;
        }

        lastCanGrab = target.CanGrab();
        promptText.text = lastCanGrab ? InteractPromptSO.Instance.Grab : InteractPromptSO.Instance.CannotGrab;
        SetVisible(true);
    }


    private void Unsubscribe()
    {
        if (interactor != null)
        {
            interactor.targetChanged -= HandleTargetChanged;
            interactor.objectChanged -= HandleObjectChanged;
        }

        if (grabber != null)
            grabber.HeldItemChanged -= HandleHeldItemChanged;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }


    private void SetVisible(bool visible)
    {
        if (isVisible == visible)
            return;

        isVisible = visible;

        if (visible)
            visibleFb?.PlayFeedbacksTopToBottom();
        else
            visibleFb?.PlayFeedbacksBottomToTop();
    }
}
