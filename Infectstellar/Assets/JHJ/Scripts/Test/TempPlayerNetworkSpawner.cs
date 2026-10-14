// [임시] PlayerScene_Item 멀티플레이 테스트용: 접속 → 고정 이름 방 입장 → 내 플레이어(Player_copied2) 생성
// 프리팹을 수정하지 않기 위해 생성 시점에 PhotonView 등 테스트 컴포넌트를 붙이는 프리팹 풀 사용, 통합 시 삭제 예정
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [임시] 씬의 빈 오브젝트에 붙여서 사용
/// 각 인스턴스가 자기 플레이어를 하나씩 PhotonNetwork.Instantiate, 카메라는 내 플레이어 ViewPivot을 따라감
/// </summary>
public class TempPlayerNetworkSpawner : MonoBehaviourPunCallbacks // [임시]
{
    [Header("프리팹")]
    [Tooltip("Resources 기준 경로 (확장자 X)")]
    [SerializeField] private string playerPrefabPath = "Player_copied2";
    [Tooltip("플레이어 루트의 직계 자식 시점 축 이름 (카메라·던지기 방향 기준)")]
    [SerializeField] private string viewPivotName = "ViewPivot";

    [Header("방")]
    [SerializeField] private string roomName = "JHJ_ItemPlayerRoom";
    [SerializeField] private byte maxPlayers = 4;
    [Tooltip("비우면 PhotonServerSettings 설정 사용. 인스턴스끼리 다른 지역에 붙으면 같은 방에 못 모이니 필요하면 지정 (예: kr)")]
    [SerializeField] private string fixedRegion = "";

    [Header("스폰")]
    [Tooltip("비우면 fallbackSpawn에서 spacing 간격으로 옆으로 배치")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Vector3 fallbackSpawn = new Vector3(11.3f, 0f, 2.1f);
    [SerializeField] private float spacing = 2f;

    [Header("카메라")]
    [Tooltip("비우면 씬에서 자동 탐색")]
    [SerializeField] private CinemachineCamera playerCamera;

    private IPunPrefabPool previousPool;


    private void Awake()
    {
        // [임시] 생성되는 플레이어에 테스트 컴포넌트를 붙이기 위해 풀 교체 (OnDestroy에서 원복)
        previousPool = PhotonNetwork.PrefabPool;
        PhotonNetwork.PrefabPool = new TestPlayerPool(playerPrefabPath, viewPivotName);

        if (playerCamera == null)
            playerCamera = FindFirstObjectByType<CinemachineCamera>();
    }

    private void OnDestroy()
    {
        PhotonNetwork.PrefabPool = previousPool;
    }

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

        Debug.Log("[TempPlayerNetworkSpawner] 접속 시도");
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
        Debug.Log($"[TempPlayerNetworkSpawner] 마스터 접속: 지역 {PhotonNetwork.CloudRegion}");
        JoinRoom();
    }

    private void JoinRoom()
    {
        PhotonNetwork.JoinOrCreateRoom(roomName, new RoomOptions { MaxPlayers = maxPlayers }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[TempPlayerNetworkSpawner] 방 입장: {PhotonNetwork.CurrentRoom.Name}, 내 Actor {PhotonNetwork.LocalPlayer.ActorNumber}, 인원 {PhotonNetwork.CurrentRoom.PlayerCount}");
        SpawnLocalPlayer();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"[TempPlayerNetworkSpawner] 방 입장 실패: {returnCode} {message}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning($"[TempPlayerNetworkSpawner] 접속 끊김: {cause}");
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
        if (player == null)
        {
            Debug.LogError($"[TempPlayerNetworkSpawner] 내 플레이어 생성 실패: {playerPrefabPath}");
            return;
        }
        Debug.Log($"[TempPlayerNetworkSpawner] 내 플레이어 생성: {player.name}, 위치 {pos}");

        // [임시] 카메라가 내 플레이어 시점 축을 따라가게
        Transform viewPivot = player.transform.Find(viewPivotName);
        if (playerCamera == null || viewPivot == null)
        {
            Debug.LogError($"[TempPlayerNetworkSpawner] 카메라 연결 실패: 카메라 {(playerCamera != null)}, {viewPivotName} {(viewPivot != null)}");
            return;
        }
        playerCamera.Target.TrackingTarget = viewPivot;
    }

    // =============================================================
    // [임시] 프리팹 풀: PUN 기본 풀과 같은 방식으로 꺼진 채 생성 → 활성화 전에 테스트 컴포넌트 추가

    private class TestPlayerPool : IPunPrefabPool // [임시]
    {
        private readonly string playerPrefabPath;
        private readonly string viewPivotName;
        private readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public TestPlayerPool(string playerPrefabPath, string viewPivotName)
        {
            this.playerPrefabPath = playerPrefabPath;
            this.viewPivotName = viewPivotName;
        }

        public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
        {
            if (!cache.TryGetValue(prefabId, out GameObject res))
            {
                res = Resources.Load<GameObject>(prefabId);
                if (res == null)
                {
                    Debug.LogError($"[TempPlayerNetworkSpawner] Resources에서 프리팹을 못 찾음: {prefabId}");
                    return null;
                }
                cache.Add(prefabId, res);
            }

            bool wasActive = res.activeSelf;
            if (wasActive) res.SetActive(false);
            GameObject instance = Object.Instantiate(res, position, rotation);
            if (wasActive) res.SetActive(true);

            if (prefabId == playerPrefabPath)
                SetupPlayer(instance);

            return instance;
        }

        public void Destroy(GameObject gameObject)
        {
            Object.Destroy(gameObject);
        }

        /// <summary>
        /// 꺼진 상태(Awake 전)에서 호출: 각 컴포넌트 Awake 시점에 PhotonView가 이미 있도록
        /// </summary>
        private void SetupPlayer(GameObject player)
        {
            // 위치·회전 동기화
            if (!player.TryGetComponent(out PhotonView view))
                view = player.AddComponent<PhotonView>();
            if (!player.TryGetComponent(out PhotonTransformView transformView))
                transformView = player.AddComponent<PhotonTransformView>();
            view.Synchronization = ViewSynchronization.UnreliableOnChange;
            view.ObservedComponents = new List<Component> { transformView };

            // 조작·입력은 일단 꺼둠 → TempNetworkPlayer.Start에서 내 플레이어만 켬
            // (남의 플레이어가 내 키보드·커서를 잡지 않도록)
            if (player.TryGetComponent(out PlayerController controller))
                controller.enabled = false;
            if (player.TryGetComponent(out PlayerInput playerInput))
                playerInput.enabled = false;

            if (!player.TryGetComponent(out TempNetworkPlayer _))
                player.AddComponent<TempNetworkPlayer>();

            // F 던지기 입력 (프리팹에 이미 붙어 있으면 그대로 사용)
            if (!player.TryGetComponent(out TempThrowInput _))
            {
                TempThrowInput throwInput = player.AddComponent<TempThrowInput>();
                throwInput.SetAimPivot(player.transform.Find(viewPivotName));
            }
        }
    }
}
