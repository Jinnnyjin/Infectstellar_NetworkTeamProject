using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;


public class PhotonLoginForTest : MonoBehaviourPunCallbacks
{
    public bool autoConnect = true;

    [SerializeField] private Canvas canvas;
    [SerializeField] private Button createBtn;
    [SerializeField] private Button joinBtn;


    public void Start()
    {
        if (autoConnect)
        {
            canvas.enabled = false;
        }
        else
        {
            canvas.enabled = true;
        }

        if (PhotonNetwork.IsConnected)
            return;

        Debug.Log("Connecting to Photon Server...");
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Master Server");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        if (autoConnect)
        {
            PhotonNetwork.JoinRandomOrCreateRoom();
        }
        else
        {
            canvas.enabled = true;
            createBtn.interactable = true;
            joinBtn.interactable = true;

            createBtn.onClick.AddListener(() =>
            {
                PhotonNetwork.CreateRoom("Room1", new RoomOptions { MaxPlayers = 4 });
            });

            joinBtn.onClick.AddListener(() =>
            {
                PhotonNetwork.JoinRandomRoom();
            });
        }
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"Joined Room: {PhotonNetwork.CurrentRoom.Name}");
        canvas.enabled = false;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.Log($"Disconnected from server: {cause}");
        canvas.enabled = true;
    }
}
