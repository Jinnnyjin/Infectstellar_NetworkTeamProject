using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using Unity.VisualScripting;
using UnityEngine;

public class RoomPanelController : MonoBehaviourPunCallbacks
{

    private Player[] players;

    [SerializeField] private PlayerPanelController[] playerPanels;

    public void Start()
    {
        RefreshPlayerList();
    }

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();
        RefreshPlayerList();

        foreach (var p in PhotonNetwork.PlayerList)
        {

            Debug.Log("00000" + p.NickName);
        }

    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        RefreshPlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
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
}
