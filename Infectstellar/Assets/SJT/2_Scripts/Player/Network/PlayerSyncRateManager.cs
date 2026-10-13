using System.Collections.Generic;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>씬의 플레이어 등록과 카메라 교체, 가장 가까운 상대에 따른 송신 빈도를 관리합니다.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class PlayerSyncRateManager : MonoBehaviour
{
    [SerializeField] private CinemachineBrain brain;
    [SerializeField] private bool useDistanceRates = true;
    [SerializeField] private bool useAdaptiveBuffer = true;
    [SerializeField, Min(0f)] private float nearEnterDistance = 10f;
    [SerializeField, Min(0f)] private float nearExitDistance = 12f;
    [SerializeField, Min(0f)] private float middleEnterDistance = 25f;
    [SerializeField, Min(0f)] private float middleExitDistance = 30f;

    private readonly List<PlayerNetworkSync> players = new List<PlayerNetworkSync>(4);
    private PlayerNetworkSync localPlayer;
    private GameObject localCamera;
    private float nextCheckTime;
    private int localRate = 30;

#if UNITY_EDITOR
    [SerializeField] private int registeredPlayers;
    [SerializeField] private int currentSendRate = 30;
    [SerializeField] private float nearestDistance;
    [SerializeField] private int pingMs;
    [SerializeField] private long outgoingPacketBytes;
    [SerializeField] private long incomingPacketBytes;
#endif

    /// <summary>현재 원격 재생의 고정·적응형 버퍼 비교 선택입니다.</summary>
    public bool UseAdaptiveBuffer => useAdaptiveBuffer;

    /// <summary>씬 수명 동안 PUN 관찰/송신의 전역 상한을 30Hz로 유지합니다.</summary>
    private void Awake()
    {
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
        if (localPlayer != null)
        {
            localPlayer.SuspendLocalControl();
        }
        SwitchCamera(null);
    }

    /// <summary>슬롯·플레이어 중복을 차단하고 로컬 카메라를 한 개만 활성화합니다.</summary>
    public bool Register(PlayerNetworkSync player)
    {
        if (player == null || brain == null || !isActiveAndEnabled)
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
        float nearest = float.PositiveInfinity;
        bool missingPosition = false;
        Vector3 position = localPlayer.Position;
        foreach (PlayerNetworkSync player in players)
        {
            if (player == null || player == localPlayer)
            {
                continue;
            }
            if (!player.HasLatestPosition)
            {
                missingPosition = true;
                continue;
            }
            nearest = Mathf.Min(nearest, Vector3.Distance(position, player.LatestPosition));
        }
        if (!useDistanceRates || missingPosition || players.Count < PhotonNetwork.CurrentRoom.PlayerCount)
        {
            localRate = 30;
        }
        else if (localRate == 30)
        {
            if (nearest > nearExitDistance)
            {
                localRate = nearest > middleExitDistance ? 10 : 15;
            }
        }
        else if (nearest <= nearEnterDistance)
        {
            localRate = 30;
        }
        else if (localRate == 15 && nearest > middleExitDistance)
        {
            localRate = 10;
        }
        else if (localRate == 10 && nearest <= middleEnterDistance)
        {
            localRate = 15;
        }
        localPlayer.SetSendRate(localRate);
#if UNITY_EDITOR
        nearestDistance = nearest;
#endif
    }
}
