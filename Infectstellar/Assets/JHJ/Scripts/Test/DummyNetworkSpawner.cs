// [임시] 네트워크 테스트용: 접속 → 고정 이름 방 입장 → 내 DummyPlayer 생성
// Multiplayer Play Mode(메인 에디터 + Virtual Player)에서 같은 방에 모이게 하기 위함, 통합 시 삭제 예정
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// [임시] 각 인스턴스가 자기 DummyPlayer를 하나씩 PhotonNetwork.Instantiate
/// 스폰 위치는 ActorNumber 순서 (1번 → spawnPoints[0])
/// </summary>
public class DummyNetworkSpawner : MonoBehaviourPunCallbacks // [임시]
{
    [Header("프리팹")]
    [Tooltip("Resources 기준 경로 (확장자 X)")]
    [SerializeField] private string playerPrefabPath = "Item_DummyPlayer";

    [Header("방")]
    [SerializeField] private string roomName = "JHJ_TestRoom";
    [SerializeField] private byte maxPlayers = 4;
    [Tooltip("비우면 PhotonServerSettings 설정 사용. 인스턴스끼리 다른 지역에 붙으면 같은 방에 못 모이니 필요하면 지정 (예: kr)")]
    [SerializeField] private string fixedRegion = "";

    [Header("스폰")]
    [Tooltip("비우면 fallbackSpawn에서 spacing 간격으로 옆으로 배치")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Vector3 fallbackSpawn = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float spacing = 2f;


    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            SpawnLocalPlayer();
            return;
        }
        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinRoom();
            return;
        }

        Debug.Log("[DummyNetworkSpawner] 접속 시도");
        if (string.IsNullOrEmpty(fixedRegion))
        {
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            // 에셋(PhotonServerSettings)을 바꾸지 않도록 복사본에만 지역 지정
            AppSettings settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.FixedRegion = fixedRegion;
            PhotonNetwork.ConnectUsingSettings(settings);
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log($"[DummyNetworkSpawner] 마스터 접속: 지역 {PhotonNetwork.CloudRegion}");
        JoinRoom();
    }

    private void JoinRoom()
    {
        PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions { MaxPlayers = maxPlayers }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[DummyNetworkSpawner] 방 입장: {PhotonNetwork.CurrentRoom.Name}, 내 Actor {PhotonNetwork.LocalPlayer.ActorNumber}, 인원 {PhotonNetwork.CurrentRoom.PlayerCount}");
        SpawnLocalPlayer();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[DummyNetworkSpawner] 방 입장 실패: {returnCode} {message}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[DummyNetworkSpawner] 접속 끊김: {cause}");
    }

    private void SpawnLocalPlayer()
    {
        int index = Mathf.Max(0, PhotonNetwork.LocalPlayer.ActorNumber - 1);

        Vector3 pos;
        Quaternion rot;
        if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[index % spawnPoints.Length] != null)
        {
            Transform point = spawnPoints[index % spawnPoints.Length];
            pos = point.position;
            rot = point.rotation;
        }
        else
        {
            pos = fallbackSpawn + Vector3.right * spacing * index;
            rot = Quaternion.identity;
        }

        GameObject player = PhotonNetwork.Instantiate(playerPrefabPath, pos, rot);
        Debug.Log($"[DummyNetworkSpawner] 내 플레이어 생성: {(player != null ? player.name : "실패")}, 위치 {pos}");
    }
}
