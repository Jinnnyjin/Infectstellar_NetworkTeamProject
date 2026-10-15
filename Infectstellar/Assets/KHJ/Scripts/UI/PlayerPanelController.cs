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
    [SerializeField] private TextMeshProUGUI nickNameTextGrid;
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
                fb.PlayFeedbacksTopToBottom();
                isShowing = true;
            }

            // 패널의 닉네임과 일치하지 않으면 변경
            if (nickNameTextGrid.text != player.NickName)
            {
                nickNameTextGrid.SetText(player.NickName);
            }
        }
    }
}
