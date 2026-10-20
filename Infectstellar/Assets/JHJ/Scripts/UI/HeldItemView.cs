using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeldItemView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private MMF_Player visibleFb;


    private bool isVisible;

    private PlayerGrabber grabber;


    public void InitialSetting(PlayerGrabber newGrabber)
    {
        if (newGrabber == null)
        {
            Debug.LogError("[HeldItemView] newGrabber가 null", this);
            return;
        }

        if (grabber != null)
            grabber.HeldItemChanged -= OnHeldItemChanged;

        grabber = newGrabber;
        grabber.HeldItemChanged += OnHeldItemChanged;
        Show(null);
    }

    private void OnHeldItemChanged(Item item)
    {
        Show(item);
    }

    private void Show(Item item)
    {
        if(item == null || item.ItemData == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        Sprite sprite = item.ItemData.Icon;

        if(sprite == null)
        {
            icon.enabled = false;
        }
        else
        {
            icon.sprite = sprite;
            icon.enabled = true;
        }

        nameText.text = item.ItemData.ItemName;
    }

    private void OnDestroy()
    {
        if(grabber != null)
            grabber.HeldItemChanged -= OnHeldItemChanged;
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
