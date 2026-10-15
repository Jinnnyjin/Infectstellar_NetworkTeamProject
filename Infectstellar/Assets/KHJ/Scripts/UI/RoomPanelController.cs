using System.Linq;
using ENZEUN.Runtime;
using Photon.Pun;
using Photon.Realtime;
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
        chatManager.ResetChatLog();
        RefreshPlayerList();
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        MenuController.Instance.ChangeToRoomSearchMenu();
    }

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

    }

    public void LeaveRoomBtnClicked()
    {
        PhotonNetwork.LeaveRoom();
    }
}
