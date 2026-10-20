using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractInfoView : MonoBehaviour
{
    [SerializeField] private Canvas slotCanvas;
    [SerializeField] private Image frameImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private RarityFrameSO rarityFrame;

    private PlayerInteractor interactor;
    private PlayerGrabber grabber;

    private void Awake()
    {
        slotCanvas.enabled = false;
    }

    public void InitialSetting(PlayerInteractor newInteractor, PlayerGrabber newGrabber)
    {
        if (newInteractor == null || newGrabber == null)
        {
            Debug.LogError("[InteractInfoView] InitialSetting 실패: interactor 또는 grabber가 null", this);
            return;
        }

        Unsubscribe();

        interactor = newInteractor;
        grabber = newGrabber;

        interactor.targetChanged += HandleTargetChanged;
        grabber.HeldItemChanged += HandleHeldItemChanged;

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
            slotCanvas.enabled = false;
            return;
        }

        IInteractable target = interactor.CurrentTarget;
        if(target == null)
        {
            slotCanvas.enabled = false;
            return;
        }

        InteractableInfo info = target.GetInfo();
        nameText.text = info.ItemName;
        priceText.text = info.Price + " G";

        if(rarityFrame != null && rarityFrame.TryGetFrame(info.Rarity, out Sprite frame) )
        {
            frameImage.sprite = frame;
        }
        else
        {
            Debug.LogWarning($"[InteractInfoView] {info.Rarity} Frame을 가져올 수 없음", this);
        }

        slotCanvas.enabled = true;
    }

    private void Unsubscribe()
    {
        if(interactor != null)
            interactor.targetChanged -= HandleTargetChanged;

        if(grabber != null)
            grabber.HeldItemChanged -= HandleHeldItemChanged;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
