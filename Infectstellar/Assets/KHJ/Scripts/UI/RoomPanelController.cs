using System.Linq;
using ENZEUN.Runtime;
using ExitGames.Client.Photon;
using MoreMountains.Feedbacks;
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
    [SerializeField] private MMF_Player loadingSceneMMF;

    private string textGameStart = "Game Start";
    private string textReady = "Ready";
    private bool isReady;

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
            gameStartBtn.onClick.RemoveListener(GameStartBtnClicked);

            isSubscribed = false;
        }
    }

    private void InitializeRoomPanel()
    {
        if (!isSubscribed)
        {
            isSubscribed = true;
            leaveRoomBtn.onClick.AddListener(LeaveRoomBtnClicked);
            gameStartBtn.onClick.AddListener(GameStartBtnClicked);
        }
    }


    #region Photon Callback
    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        ResetMyReadyState();

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

        ResetMyReadyState();

        MenuController.Instance.ChangeToRoomSearchMenu();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        base.OnMasterClientSwitched(newMasterClient);

        ResetMyReadyState();

        RefreshPlayerList();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        if (changedProps.ContainsKey(PhotonLinkManager.isReady_Customproperty))
        {
            bool newReady = (bool)changedProps[PhotonLinkManager.isReady_Customproperty];

#if UNITY_EDITOR
            Debug.Log($"[방관리자] {targetPlayer.NickName}의 준비 상태 변경됨: {newReady}");
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
        players = PhotonNetwork.PlayerListOthers;

        bool allReady = true;

        if (players == null || players.Length <= 0)
        {
            allReady = false;
        }
        else
        {
            foreach (var p in players)
            {
                if (p.CustomProperties.TryGetValue(PhotonLinkManager.isReady_Customproperty, out object obj))
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

    public void GameStartBtnClicked()
    {
        if (PhotonNetwork.LocalPlayer.IsMasterClient)
        {
            photonView.RPC(nameof(StartLoadScene), RpcTarget.All);
        }
        else
        {
            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("IsReady", out object obj))
            {
                isReady = (bool)obj;
            }
            PhotonLinkManager.Instance.SetMyReadyState(!isReady);
        }
    }

    public void ResetMyReadyState()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(PhotonLinkManager.isReady_Customproperty, out object obj))
        {
            if ((bool)obj)
            {
                PhotonLinkManager.Instance.SetMyReadyState(false);
            }
        }
    }

    [PunRPC]
    public void StartLoadScene(PhotonMessageInfo info)
    {
        loadingSceneMMF.PlayFeedbacks();
    }

    public void LeaveRoomBtnClicked()
    {
        PhotonNetwork.LeaveRoom();
    }
}
