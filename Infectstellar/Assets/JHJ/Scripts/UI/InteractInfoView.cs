using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractInfoView : MonoBehaviour
{
    [Header("아이템 (IInteractable)")]
    [SerializeField] private GameObject itemFrame;
    [SerializeField] private Image frameImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private RarityFrameSO rarityFrame;

    [Header("일반 오브젝트 (IInteractObject)")]
    [SerializeField] private GameObject objectFrame;
    [SerializeField] private TMP_Text descriptionText;

    [SerializeField] private MMF_Player visibleFb;   // 정방향 = 표시, 역방향 = 숨김

    private bool isVisible;
    private bool isItemFrame = true;   // 현재 켜진 프레임 (true = 아이템)

    private PlayerInteractor interactor;
    private PlayerGrabber grabber;


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
        interactor.objectChanged += HandleObjectChanged;
        grabber.HeldItemChanged += HandleHeldItemChanged;

        Refresh();
    }

    private void HandleTargetChanged(IInteractable interactable)
    {
        Refresh();
    }

    private void HandleObjectChanged(IInteractObject obj)
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
            SetVisible(false);
            return;
        }

        IInteractable target = interactor.CurrentTarget;
        if (target != null)
        {
            ShowItem(target);
            return;
        }

        // 설명이 비어있는 오브젝트는 표시 안 함
        IInteractObject obj = interactor.CurrentObject;
        if (obj != null && !string.IsNullOrEmpty(obj.Description))
        {
            ShowObject(obj);
            return;
        }

        SetVisible(false);
    }

    private void ShowItem(IInteractable target)
    {
        SetFrame(isItem: true);

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

        SetVisible(true);
    }

    private void ShowObject(IInteractObject obj)
    {
        SetFrame(isItem: false);

        descriptionText.text = obj.Description;

        SetVisible(true);
    }

    /// <summary>
    /// 아이템 / 일반 오브젝트 프레임 중 하나만 켬
    /// 표시 중에 프레임 종류가 바뀌면 표시 연출 다시 재생
    /// </summary>
    private void SetFrame(bool isItem)
    {
        bool isFrameChanged = isItem != isItemFrame;
        isItemFrame = isItem;

        if (itemFrame != null)
            itemFrame.SetActive(isItem);

        if (objectFrame != null)
            objectFrame.SetActive(!isItem);

        if (isVisible && isFrameChanged && visibleFb != null)
        {
            visibleFb.StopFeedbacks();
            visibleFb.PlayFeedbacksTopToBottom();
        }
    }

    private void Unsubscribe()
    {
        if (interactor != null)
        {
            interactor.targetChanged -= HandleTargetChanged;
            interactor.objectChanged -= HandleObjectChanged;
        }

        if(grabber != null)
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
