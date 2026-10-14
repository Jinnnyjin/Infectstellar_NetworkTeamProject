using System.Collections.Generic;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>씬 수명의 싱글톤으로 플레이어 등록·카메라 교체·거리별 송신 빈도를 관리합니다.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class PlayerSyncRateManager : MonoBehaviour
{
    private const float c_minRateCheckInterval = 0.001f; // 거리 검사 간격의 양수 하한(초)

    private static PlayerSyncRateManager instance; // 현재 클라이언트에서 사용할 하나의 씬 관리자

    [Header("로컬 카메라")]
    [SerializeField] private CinemachineBrain brain; // Main Camera에서 플레이어 카메라 전환을 처리할 컴포넌트
    [Header("거리별 전송·재생 옵션")]
    [SerializeField] private bool useDistanceRates = true; // 거리에 따라 전송 빈도를 바꿀지 여부(끄면 항상 30Hz)
    [SerializeField] private bool useAdaptiveBuffer = true; // 전송 빈도와 도착 흔들림에 맞춰 보간 지연을 조절할지 여부
    [SerializeField, Min(0f)] private float nearEnterDistance = 10f; // 상대가 이 거리 안으로 오면 30Hz로 올리는 기준(m)
    [SerializeField, Min(0f)] private float nearExitDistance = 12f; // 30Hz 상태에서 상대가 이 거리 밖으로 가면 낮추는 기준(m)
    [SerializeField, Min(0f)] private float middleEnterDistance = 25f; // 10Hz 상태에서 상대가 이 거리 안으로 오면 15Hz 이상으로 올리는 기준(m)
    [SerializeField, Min(0f)] private float middleExitDistance = 30f; // 30/15Hz 상태에서 상대가 이 거리 밖으로 가면 10Hz로 낮추는 기준(m)
    [SerializeField, Min(c_minRateCheckInterval), Tooltip("거리별 전송 등급과 진단을 다시 확인하는 간격(초)")]
    private float rateCheckInterval = 0.1f; // 거리 등급과 진단 갱신 사이의 시간(초)

    private readonly List<PlayerNetworkSync> players = new List<PlayerNetworkSync>(PlayerNetworkConstants.c_spawnSlotCount); // 씬에 등록된 네트워크 복제본
    private PlayerNetworkSync localPlayer; // 현재 내가 조종하는 플레이어
    private GameObject localCamera; // 현재 켜져 있는 내 플레이어 카메라
    private float nextCheckTime; // 다음 거리 검사 시각(게임 시작 후 실제 경과 시간, 초)
    private int localRate = PlayerNetworkConstants.c_highSendRate; // 거리 판정으로 선택한 내 플레이어의 전송 목표 횟수(Hz)

#if UNITY_EDITOR
    [Header("연결 진단값")]
    [SerializeField] private int registeredPlayers; // Inspector에서 확인할 등록된 플레이어 수
    [SerializeField] private int currentSendRate = PlayerNetworkConstants.c_highSendRate; // Inspector에서 확인할 현재 전송 목표 횟수(Hz)
    [SerializeField] private float nearestDistance; // 가장 가까운 상대까지의 거리(m, 상대가 없으면 무한대)
    [SerializeField] private int pingMs; // Photon 서버와 왕복하는 데 걸리는 시간(ms)
    [SerializeField] private long outgoingPacketBytes; // PUN이 집계한 누적 송신 패킷 크기(바이트)
    [SerializeField] private long incomingPacketBytes; // PUN이 집계한 누적 수신 패킷 크기(바이트)
#endif

    /// <summary>이 클라이언트의 씬 관리자를 반환합니다. 생성 전·파괴 후에는 null입니다.</summary>
    public static PlayerSyncRateManager Instance => instance;

    /// <summary>현재 원격 재생의 고정·적응형 버퍼 비교 선택입니다.</summary>
    public bool UseAdaptiveBuffer => useAdaptiveBuffer;

    /// <summary>거리 검사 간격이 양수가 되도록 Inspector 설정을 보정합니다.</summary>
    private void OnValidate()
    {
        rateCheckInterval = Mathf.Max(c_minRateCheckInterval, rateCheckInterval);
    }

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

        PhotonNetwork.SendRate = PlayerNetworkConstants.c_highSendRate;
        PhotonNetwork.SerializationRate = PlayerNetworkConstants.c_highSendRate;
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

    /// <summary>거리와 진단을 설정된 검사 간격으로 갱신합니다.</summary>
    private void Update()
    {
        if (Time.unscaledTime < nextCheckTime)
        {
            return;
        }
        nextCheckTime = Time.unscaledTime + rateCheckInterval;
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
            var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer; // 클라이언트 전체 송수신 통계를 제공하는 연결
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

    #region 플레이어 등록·슬롯·카메라

    /// <summary>슬롯·플레이어 중복을 차단하고 로컬 카메라를 한 개만 활성화합니다.</summary>
    public bool Register(PlayerNetworkSync player) // 생성 초기화를 마친 등록 대상
    {
        if (instance != this || player == null || brain == null || !isActiveAndEnabled)
        {
            return false;
        }
        // 동일 객체의 재등록은 초기화를 반복하지 않는다. 다른 객체의 Actor/슬롯 중복은 차단한다.
        if (players.Contains(player))
        {
            return true;
        }
        foreach (PlayerNetworkSync existing in players) // Actor와 시작 슬롯이 겹치는지 비교할 기존 복제본
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
            localRate = PlayerNetworkConstants.c_highSendRate;
            SwitchCamera(player.PlayerCamera);
        }
        return true;
    }

    /// <summary>파괴된 플레이어를 목록과 로컬 카메라에서 제거합니다.</summary>
    public void Unregister(PlayerNetworkSync player) // 파괴되어 등록을 해제할 대상
    {
        players.Remove(player);
        if (localPlayer == player)
        {
            player.SuspendLocalControl();
            localPlayer = null;
            SwitchCamera(null);
        }
    }

    /// <summary>ActorNumber에 해당하는 생성 콜백이 등록됐는지 확인합니다.</summary>
    public bool HasActor(int actorNumber) // 방 안에서 플레이어를 구분하는 번호
    {
        foreach (PlayerNetworkSync player in players) // 생성 등록 여부를 조회할 복제본
        {
            if (player != null && player.ActorNumber == actorNumber)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>등록 목록에서 첫 빈 시작 슬롯을 선택합니다.</summary>
    public int FindFreeSlot()
    {
        for (int slot = 0; slot < PlayerNetworkConstants.c_spawnSlotCount; slot++) // 비어 있는지 검사할 시작 위치 번호
        {
            if (!IsSlotOccupied(slot))
            {
                return slot;
            }
        }
        return PlayerNetworkConstants.c_unassignedSpawnSlot;
    }

    /// <summary>지정한 시작 슬롯을 사용하는 유효 복제본이 있는지 확인합니다.</summary>
    private bool IsSlotOccupied(int slot) // 검사할 시작 위치 번호
    {
        foreach (PlayerNetworkSync player in players) // 슬롯을 사용 중인지 확인할 복제본
        {
            if (player != null && player.SpawnSlot == slot)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>이전 카메라를 먼저 끈 뒤 새 카메라를 켜고 Brain의 블렌드 기록을 초기화합니다.</summary>
    private void SwitchCamera(GameObject nextCamera) // 새로 켤 내 플레이어 카메라; null이면 기존 카메라만 해제
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

    #endregion

    #region 거리별 전송 등급

    /// <summary>상대의 최신 수신 위치로 거리 히스테리시스를 적용합니다. 초기 정보 부족 시 30Hz입니다.</summary>
    private void UpdateRate()
    {
        float nearest; // 가장 가까운 상대의 최신 수신 위치까지 거리(m)
        bool allPositionsKnown = TryGetNearestDistance(out nearest); // 모든 상대의 위치가 한 번 이상 수신됐는지 여부
        bool allPlayersRegistered = players.Count >= PhotonNetwork.CurrentRoom.PlayerCount; // 입장한 모든 Actor의 복제본을 등록했는지 여부
        // 초기 정보가 부족할 때는 낮은 등급을 추측하지 않고 기존 30Hz 전송을 유지한다.
        if (!useDistanceRates || !allPositionsKnown || !allPlayersRegistered)
        {
            localRate = PlayerNetworkConstants.c_highSendRate;
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

    /// <summary>가장 가까운 상대 거리를 구하고 모든 상대의 최신 위치가 준비됐는지 반환합니다.</summary>
    private bool TryGetNearestDistance(
        out float nearest) // 가장 가까운 상대 거리(m; 상대가 없으면 무한대)
    {
        nearest = float.PositiveInfinity;
        bool allPositionsKnown = true; // 수신 위치가 아직 없는 상대가 존재하는지 판정할 기준
        Vector3 position = localPlayer.Position; // 내 현재 위치; 원격 표시 지연을 넣지 않는 거리 기준(m)
        foreach (PlayerNetworkSync player in players) // 거리 계산과 초기 수신 여부를 확인할 상대
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
            // 원격 Transform은 조금 전 움직임을 표시한다. 거리 판정은 최신 수신 위치로 계산한다.
            nearest = Mathf.Min(nearest, Vector3.Distance(position, player.LatestPosition));
        }
        return allPositionsKnown;
    }

    /// <summary>경계에서 등급이 반복 전환되지 않도록 서로 다른 진입·이탈 거리를 사용합니다.</summary>
    private int SelectSendRate(
        float nearestDistance) // 가장 가까운 상대까지의 거리(m)
    {
        if (localRate == PlayerNetworkConstants.c_highSendRate)
        {
            if (nearestDistance > nearExitDistance)
            {
                return nearestDistance > middleExitDistance
                    ? PlayerNetworkConstants.c_lowSendRate
                    : PlayerNetworkConstants.c_middleSendRate;
            }
            return PlayerNetworkConstants.c_highSendRate;
        }

        // 한 번 높은 등급에서 내려온 뒤에는 더 가까운 진입 기준까지 와야 다시 올린다.
        if (nearestDistance <= nearEnterDistance)
        {
            return PlayerNetworkConstants.c_highSendRate;
        }
        if (localRate == PlayerNetworkConstants.c_middleSendRate && nearestDistance > middleExitDistance)
        {
            return PlayerNetworkConstants.c_lowSendRate;
        }
        if (localRate == PlayerNetworkConstants.c_lowSendRate && nearestDistance <= middleEnterDistance)
        {
            return PlayerNetworkConstants.c_middleSendRate;
        }
        return localRate;
    }

    #endregion
}
