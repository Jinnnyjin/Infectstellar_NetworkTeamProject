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
    [SerializeField] private CanvasGroup masterIconCanvas;


    private bool isShowing = false;


    public void RefreshPlayerPanel()
    {
        // 1. 플레이어가 존재하지 않을 경우
        if (player == null)
        {
            if (isShowing)
            {
                fb.PlayFeedbacksBottomToTop();
                isShowing = false;
                gameObject.SetActive(false);
            }
            return;
        }

        // 2. 존재할 경우
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

        // 마스터면 아이콘 보여주기
        if (player.IsMasterClient)
        {
            masterIconCanvas.alpha = 1f;
        }
        else
        {
            masterIconCanvas.alpha = 0f;
        }
    }

}
