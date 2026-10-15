using System.Linq;
using ENZEUN.Runtime;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class RoomPanelController : MonoBehaviourPunCallbacks
{

    private Player[] players;

    [SerializeField] private ChatManager chatManager;
    [SerializeField] private PlayerPanelController[] playerPanels;
    [SerializeField] private Button gameStartBtn;
    [SerializeField] private Button leaveRoomBtn;
    [SerializeField] private TextMeshProUGUI gameStartText;

    private string textGameStart = "Game Start";
    private string textReady = "Ready";

    private bool isSubscribed = false;


    public void Start()
    {
        RefreshPlayerList();
    }

    public override void OnDisable()
    {
        base.OnDisable();

        if (isSubscribed)
        {
            leaveRoomBtn.onClick.RemoveListener(LeaveRoomBtnClicked);

            isSubscribed = false;
        }
    }

    private void InitializeRoomPanel()
    {
        if (!isSubscribed)
        {
            leaveRoomBtn.onClick.AddListener(LeaveRoomBtnClicked);

            isSubscribed = true;
        }

    }


    #region Photon Callback
    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        chatManager.ResetChatLog();

        RefreshPlayerList();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        chatManager.AddChatMessage("System", $"player [{newPlayer.NickName}] has joined room.");
        RefreshPlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        chatManager.AddChatMessage("System", $"player [{otherPlayer.NickName}] has left room.");
        RefreshPlayerList();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        chatManager.ResetChatLog();

        MenuController.Instance.ChangeToRoomSearchMenu();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        base.OnMasterClientSwitched(newMasterClient);

        RefreshPlayerList();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (changedProps.ContainsKey("IsReady"))
        {
            bool newReady = (bool)changedProps["IsReady"];

#if UNITY_EDITOR
            Debug.Log($"[포톤] {targetPlayer.NickName}의 준비 상태 변경됨: {newReady}");
#endif
        }

        RefreshPlayerList();
    }

    #endregion


    private void RefreshPlayerList()
    {
        InitializeRoomPanel();

        if (!PhotonNetwork.IsConnected)
        {
            foreach (var panel in playerPanels)
            {
                panel.player = null;
                panel.RefreshPlayerPanel();
            }
            return;
        }

        players = PhotonNetwork.PlayerList;

        int loop = players.Length;

        for (int i = 0; i < 4; i++)
        {
            if (i < loop)
            {
                if (players[i] != null)
                {
                    playerPanels[i].player = players[i];
                }
                else
                {
                    playerPanels[i].player = null;
                }
            }
            else
            {
                playerPanels[i].player = null;
            }
            playerPanels[i].RefreshPlayerPanel();
        }

        // 게임시작-게임준비 버튼 전환
        // 1. 마스터일 경우
        if (PhotonNetwork.LocalPlayer.IsMasterClient)
        {
            if (gameStartText.text != textGameStart)
            {
                gameStartText.SetText(textGameStart);
            }
            CheckPlayerReady();
        }
        // 2. 마스터가 아닐 경우
        else
        {
            if (gameStartText.text != textReady)
            {
                gameStartText.SetText(textReady);
            }
        }
    }

    public void CheckPlayerReady()
    {
        players = PhotonNetwork.PlayerList;
        bool allReady = true;

        if (players.Length <= 1)
        {
            allReady = false;
        }
        else
        {
            foreach (var p in players)
            {
                if (p.CustomProperties.TryGetValue("IsReady", out object obj))
                {
                    if (!(bool)obj)
                    {
                        allReady = false;
                    }
                }
                else
                {
                    allReady = false;
                }
            }
        }
        gameStartBtn.interactable = allReady;
    }

    public void LeaveRoomBtnClicked()
    {
        PhotonNetwork.LeaveRoom();
    }
}
