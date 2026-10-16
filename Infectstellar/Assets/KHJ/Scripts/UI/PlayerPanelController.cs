using UnityEngine;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;
using MoreMountains.Feedbacks;
using ENZEUN.Runtime;
using Photon.Pun;
using System;
using System.Collections.Generic;


public class PlayerPanelController : MonoBehaviour
{
    public Player player;
    [SerializeField] private TextMeshProUGUI nickNameTextGrid;
    [SerializeField] private Button colorBtn;
    [SerializeField] private MMF_Player fb;
    [SerializeField] private CanvasGroup masterIconCanvas;
    [SerializeField] private CanvasGroup readyIconCanvas;



    private bool isShowing = false;
    private bool isInitialized = false;
    private int currentColorIndex = -1;


    private void Start()
    {
        if (!isInitialized)
        {
            colorBtn.onClick.AddListener(RequestChangeColor);

            RequestChangeColor();
        }
    }

    private void RequestChangeColor()
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (!player.IsLocal)
        {
            return;
        }

        int colorLength = Enum.GetValues(typeof(PhotonLinkManager.PlayerColor)).Length;

        int targetColorIndex = currentColorIndex;

        Player[] players = PhotonNetwork.PlayerListOthers;

        // 만약 자신 제외 플레이어가 0명 이라면, 다음 색상으로 변경
        if (players == null || players.Length <= 0)
        {
            targetColorIndex = (targetColorIndex + 1) % colorLength;
        }

        // 플레이어가 1명 이상이면
        else
        {
            bool canChange = false;

            List<int> colors = new();
            // 모든 색을 가져와서 저장 
            foreach (var p in players)
            {
                if (p.CustomProperties.TryGetValue(PhotonLinkManager.myColor_Customproperty, out object obj))
                {
                    colors.Add((int)obj);
                }
            }

            int safetyCounter = 0; // 무한 루프 방지용 안전장치

            // 후 내 타깃 Index 와 비교, 겹치지 않도록 설정
            while (!canChange && safetyCounter < colorLength + 10)
            {
                targetColorIndex = (targetColorIndex + 1) % colorLength;
                safetyCounter++;

                bool hasColor = false;

                foreach (var idx in colors)
                {

                    if (idx == targetColorIndex)
                    {
                        hasColor = true;
                        break;
                    }
                }
                canChange = !hasColor;
            }
        }

        PhotonLinkManager.Instance.SetMyColorState(targetColorIndex);

        currentColorIndex = targetColorIndex;
    }

    private void ChangeColor()
    {
        colorBtn.image.color = PhotonLinkManager.GetCurrentColor();
    }


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
            readyIconCanvas.alpha = 0f;
            readyIconCanvas.blocksRaycasts = false;
        }
        else
        {
            masterIconCanvas.alpha = 0f;

            // 현재 Ready 상태 보여주기 (마스터가 아닌 애들만 표시)
            if (player.CustomProperties.TryGetValue("IsReady", out object obj))
            {
                bool show = (bool)obj;

                float alpha;

                if (show)
                {
                    alpha = 1f;
                }
                else
                {
                    alpha = 0f;
                }
                readyIconCanvas.alpha = alpha;
                readyIconCanvas.blocksRaycasts = show;
            }
        }

        ChangeColor();

    }

}
