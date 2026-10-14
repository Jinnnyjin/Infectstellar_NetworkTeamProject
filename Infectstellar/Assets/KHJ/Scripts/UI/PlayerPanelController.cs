using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.UI;
using MoreMountains.Feedbacks;


public class PlayerPanelController : MonoBehaviour
{
    public Player player;
    [SerializeField] private TMP_Text nickNameTextGrid;
    [SerializeField] private Image colorGrid;
    [SerializeField] private MMF_Player fb;

    private bool isShowing = false;


    public void RefreshPlayerPanel()
    {
        if (player == null)
        {
            if (isShowing)
            {
                fb.PlayFeedbacksBottomToTop();
                isShowing = false;
                gameObject.SetActive(false);
            }
        }
        else
        {
            if (!isShowing)
            {
                gameObject.SetActive(true);
                nickNameTextGrid.SetText(player.NickName);
                fb.PlayFeedbacksTopToBottom();
                isShowing = true;
            }
        }
    }
}
