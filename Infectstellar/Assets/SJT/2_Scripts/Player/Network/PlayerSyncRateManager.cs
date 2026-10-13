using System.Collections.Generic;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>씬 수명의 싱글톤으로 플레이어 등록·카메라 교체·거리별 송신 빈도를 관리합니다.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class PlayerSyncRateManager : MonoBehaviour
{
    private static PlayerSyncRateManager instance; // 현재 클라이언트에서 사용할 하나의 씬 관리자

    [SerializeField] private CinemachineBrain brain; // Main Camera에서 플레이어 카메라 전환을 처리할 컴포넌트
    [SerializeField] private bool useDistanceRates = true; // 거리에 따라 전송 빈도를 바꿀지 여부(끄면 항상 30Hz)
    [SerializeField] private bool useAdaptiveBuffer = true; // 전송 빈도와 도착 흔들림에 맞춰 보간 지연을 조절할지 여부
    [SerializeField, Min(0f)] private float nearEnterDistance = 10f; // 상대가 이 거리 안으로 오면 30Hz로 올리는 기준(m)
    [SerializeField, Min(0f)] private float nearExitDistance = 12f; // 30Hz 상태에서 상대가 이 거리 밖으로 가면 낮추는 기준(m)
    [SerializeField, Min(0f)] private float middleEnterDistance = 25f; // 10Hz 상태에서 상대가 이 거리 안으로 오면 15Hz 이상으로 올리는 기준(m)
    [SerializeField, Min(0f)] private float middleExitDistance = 30f; // 30/15Hz 상태에서 상대가 이 거리 밖으로 가면 10Hz로 낮추는 기준(m)

    private readonly List<PlayerNetworkSync> players = new List<PlayerNetworkSync>(4); // 이 씬에 등록된 네트워크 플레이어 목록
    private PlayerNetworkSync localPlayer; // 현재 내가 조종하는 플레이어
    private GameObject localCamera; // 현재 켜져 있는 내 플레이어 카메라
    private float nextCheckTime; // 다음 거리 검사 시각(게임 시작 후 실제 경과 시간, 초)
    private int localRate = 30; // 거리 판정으로 선택한 내 플레이어의 전송 목표 횟수(Hz)

#if UNITY_EDITOR
    [SerializeField] private int registeredPlayers; // Inspector에서 확인할 등록된 플레이어 수
    [SerializeField] private int currentSendRate = 30; // Inspector에서 확인할 현재 전송 목표 횟수(Hz)
    [SerializeField] private float nearestDistance; // 가장 가까운 상대까지의 거리(m, 상대가 없으면 무한대)
    [SerializeField] private int pingMs; // Photon 서버와 왕복하는 데 걸리는 시간(ms)
    [SerializeField] private long outgoingPacketBytes; // PUN이 집계한 누적 송신 패킷 크기(바이트)
    [SerializeField] private long incomingPacketBytes; // PUN이 집계한 누적 수신 패킷 크기(바이트)
#endif

    /// <summary>이 클라이언트의 씬 관리자를 반환합니다. 생성 전·파괴 후에는 null입니다.</summary>
    public static PlayerSyncRateManager Instance => instance;

    /// <summary>현재 원격 재생의 고정·적응형 버퍼 비교 선택입니다.</summary>
    public bool UseAdaptiveBuffer => useAdaptiveBuffer;

    /// <summary>중복을 차단하고 필수 참조가 준비된 관리자를 싱글톤으로 등록합니다.</summary>
    private void Awake()
    {
        if (instance != null && instance != this)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerSyncRateManager] 이미 등록된 씬 관리자가 있어 중복 관리자를 비활성화합니다.", this);
#endif
            enabled = false;
            return;
        }

        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 30;
#if UNITY_EDITOR
        PhotonNetwork.NetworkStatisticsEnabled = true;
#endif
        if (brain == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerSyncRateManager] Main Camera의 CinemachineBrain을 연결해야 합니다.", this);
#endif
            enabled = false;
            return;
        }

        instance = this;
    }

    /// <summary>중복 관리자를 다시 켜더라도 싱글톤 외의 관리자는 동작하지 않게 합니다.</summary>
    private void OnEnable()
    {
        if (instance != this)
        {
            enabled = false;
        }
    }

    /// <summary>거리와 진단을 0.1초 간격으로 갱신합니다.</summary>
    private void Update()
    {
        if (Time.unscaledTime < nextCheckTime)
        {
            return;
        }
        nextCheckTime = Time.unscaledTime + 0.1f;
        if (localPlayer != null && localPlayer.IsLocalControlActive)
        {
            UpdateRate();
        }
#if UNITY_EDITOR
        registeredPlayers = players.Count;
        currentSendRate = localRate;
        if (PhotonNetwork.IsConnected)
        {
            pingMs = PhotonNetwork.GetPing();
            var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
            outgoingPacketBytes = peer.TrafficStatsOutgoing.TotalPacketBytes;
            incomingPacketBytes = peer.TrafficStatsIncoming.TotalPacketBytes;
        }
#endif
    }

    /// <summary>관리자가 비활성화되면 자신이 관리하던 로컬 입력과 카메라를 해제합니다.</summary>
    private void OnDisable()
    {
        if (instance != this)
        {
            return;
        }

        if (localPlayer != null)
        {
            localPlayer.SuspendLocalControl();
        }
        SwitchCamera(null);
    }

    /// <summary>자신이 등록한 싱글톤 참조만 해제하여 다음 씬의 관리자가 등록될 수 있게 합니다.</summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>슬롯·플레이어 중복을 차단하고 로컬 카메라를 한 개만 활성화합니다.</summary>
    public bool Register(PlayerNetworkSync player)
    {
        if (instance != this || player == null || brain == null || !isActiveAndEnabled)
        {
            return false;
        }
        if (players.Contains(player))
        {
            return true;
        }
        foreach (PlayerNetworkSync existing in players)
        {
            if (existing != null && (existing.ActorNumber == player.ActorNumber || existing.SpawnSlot == player.SpawnSlot))
            {
#if UNITY_EDITOR
                Debug.LogError("[PlayerSyncRateManager] 중복 플레이어 또는 시작 슬롯입니다.", player);
#endif
                return false;
            }
        }
        players.Add(player);
        if (player.IsLocalControlActive)
        {
            if (localPlayer != null && localPlayer != player)
            {
                localPlayer.SuspendLocalControl();
            }
            localPlayer = player;
            localRate = 30;
            SwitchCamera(player.PlayerCamera);
        }
        return true;
    }

    /// <summary>파괴된 플레이어를 목록과 로컬 카메라에서 제거합니다.</summary>
    public void Unregister(PlayerNetworkSync player)
    {
        players.Remove(player);
        if (localPlayer == player)
        {
            player.SuspendLocalControl();
            localPlayer = null;
            SwitchCamera(null);
        }
    }

    /// <summary>이 ActorNumber의 생성 콜백이 등록됐는지 확인합니다.</summary>
    public bool HasActor(int actorNumber)
    {
        foreach (PlayerNetworkSync player in players)
        {
            if (player != null && player.ActorNumber == actorNumber)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>모든 복제본이 알고 있는 생성 슬롯 중 첫 빈 슬롯을 선택합니다.</summary>
    public int FindFreeSlot()
    {
        for (int slot = 0; slot < 4; slot++)
        {
            bool occupied = false;
            foreach (PlayerNetworkSync player in players)
            {
                if (player != null && player.SpawnSlot == slot)
                {
                    occupied = true;
                    break;
                }
            }
            if (!occupied)
            {
                return slot;
            }
        }
        return -1;
    }

    /// <summary>이전 카메라를 먼저 끈 뒤 새 카메라를 켜고 Brain의 블렌드 기록을 초기화합니다.</summary>
    private void SwitchCamera(GameObject nextCamera)
    {
        if (localCamera != null)
        {
            localCamera.SetActive(false);
        }
        localCamera = nextCamera;
        if (localCamera != null)
        {
            localCamera.SetActive(true);
        }
        if (brain != null)
        {
            brain.ResetState();
        }
    }

    /// <summary>상대의 최신 수신 위치로 거리 히스테리시스를 적용합니다. 초기 정보 부족 시 30Hz입니다.</summary>
    private void UpdateRate()
    {
        bool allPositionsKnown = TryGetNearestDistance(out float nearest);
        bool allPlayersRegistered = players.Count >= PhotonNetwork.CurrentRoom.PlayerCount;
        if (!useDistanceRates || !allPositionsKnown || !allPlayersRegistered)
        {
            localRate = 30;
        }
        else
        {
            localRate = SelectSendRate(nearest);
        }

        localPlayer.SetSendRate(localRate);
#if UNITY_EDITOR
        nearestDistance = nearest;
#endif
    }

    /// <summary>가장 가까운 상대까지의 거리를 구하고, 아직 수신 위치가 없는 상대도 확인합니다.</summary>
    private bool TryGetNearestDistance(out float nearest)
    {
        nearest = float.PositiveInfinity;
        bool allPositionsKnown = true;
        Vector3 position = localPlayer.Position;
        foreach (PlayerNetworkSync player in players)
        {
            if (player == null || player == localPlayer)
            {
                continue;
            }
            if (!player.HasLatestPosition)
            {
                allPositionsKnown = false;
                continue;
            }
            nearest = Mathf.Min(nearest, Vector3.Distance(position, player.LatestPosition));
        }
        return allPositionsKnown;
    }

    /// <summary>현재 등급의 이탈·진입 거리로 30/15/10Hz 중 다음 등급을 선택합니다.</summary>
    private int SelectSendRate(float nearest)
    {
        if (localRate == 30)
        {
            if (nearest > nearExitDistance)
            {
                if (nearest > middleExitDistance)
                {
                    return 10;
                }
                return 15;
            }
            return 30;
        }
        if (nearest <= nearEnterDistance)
        {
            return 30;
        }
        if (localRate == 15 && nearest > middleExitDistance)
        {
            return 10;
        }
        if (localRate == 10 && nearest <= middleEnterDistance)
        {
            return 15;
        }
        return localRate;
    }
}
