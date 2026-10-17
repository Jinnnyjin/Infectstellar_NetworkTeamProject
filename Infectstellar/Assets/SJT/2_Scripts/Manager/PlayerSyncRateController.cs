using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
#if UNITY_EDITOR
using System.Text;
#endif

/// <summary>다른 사용자의 집중 대상과의 거리에 따라 현재 클라이언트의 직렬화 빈도를 조절합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerNetworkSyncManager))]
public sealed class PlayerSyncRateController : MonoBehaviour
{
    private const string c_viewActorKey = "ViewActor";
    private const float c_propertyRetryInterval = 0.5f;
#if UNITY_EDITOR
    private const string c_debugRateKey = "DebugSerializationRate";
#endif

    [SerializeField, Min(1), Tooltip("가까운 상대가 있을 때 사용할 초당 직렬화 횟수입니다.")]
    private int nearSerializationRate = 30;

    [SerializeField, Range(0.1f, 1f), Tooltip("모든 상대가 멀 때 근거리 기준값에 적용할 배율입니다.")]
    private float farRateMultiplier = 0.5f;

    [SerializeField, Min(0f), Tooltip("이 거리 안으로 들어오면 근거리 빈도로 복귀합니다. 단위: m")]
    private float nearDistance = 15f;

    [SerializeField, Min(0f), Tooltip("원거리로 전환할 때 추가하는 완충 거리입니다. 단위: m")]
    private float distanceMargin = 3f;

    [SerializeField, Min(0.01f), Tooltip("거리 판단 간격입니다. 단위: 초")]
    private float checkInterval = 0.1f;

    private PlayerNetworkSyncManager syncManager;

    // 거리별 빈도의 기준이 아니라 비활성화 시 복원할 값입니다.
    private int previousSerializationRate;
    private int lastRequestedViewActor = -1;
    private bool isFar;
    private float nextCheckTime;
    private float nextPropertyRetryTime;
#if UNITY_EDITOR
    private readonly StringBuilder debugRatesText = new StringBuilder(256);
    private int lastRequestedDebugRate = -1;
    private float nextDebugPropertyRetryTime;
#endif

    /// <summary>같은 객체의 플레이어 관리자를 확인합니다.</summary>
    private void Awake()
    {
        syncManager = GetComponent<PlayerNetworkSyncManager>();
        if (syncManager == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerSyncRateController] PlayerNetworkSyncManager가 필요합니다.", this);
#endif
            enabled = false;
        }
    }

    /// <summary>복원할 빈도를 보관하고 집중 대상 변경 구독과 최초 처리를 예약합니다.</summary>
    private void OnEnable()
    {
        if (syncManager == null)
        {
            return;
        }

        previousSerializationRate = PhotonNetwork.SerializationRate;
        isFar = false;
#if UNITY_EDITOR
        lastRequestedDebugRate = -1;
        nextDebugPropertyRetryTime = 0f;
#endif

        syncManager.D_FocusedPlayerChanged += HandleFocusedPlayerChanged;
        HandleFocusedPlayerChanged(syncManager.FocusedPlayerSync);
    }

    /// <summary>구독을 해제하고 이전 빈도와 집중 대상 공유 상태를 정리합니다.</summary>
    private void OnDisable()
    {
        if (syncManager != null)
        {
            syncManager.D_FocusedPlayerChanged -= HandleFocusedPlayerChanged;
        }

        if (previousSerializationRate > 0 && PhotonNetwork.SerializationRate != previousSerializationRate)
        {
            PhotonNetwork.SerializationRate = previousSerializationRate;
        }

        Player localPhotonPlayer = PhotonNetwork.LocalPlayer;
        if (localPhotonPlayer == null || previousSerializationRate <= 0)
        {
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            // 0은 현재 집중 대상 정보를 사용할 수 없다는 뜻입니다.
            localPhotonPlayer.SetCustomProperties(new Hashtable
            {
                [c_viewActorKey] = 0,
#if UNITY_EDITOR
                [c_debugRateKey] = 0,
#endif
            });
        }
        else
        {
            PhotonNetwork.RemovePlayerCustomProperties(new[] { c_viewActorKey });
#if UNITY_EDITOR
            PhotonNetwork.RemovePlayerCustomProperties(new[] { c_debugRateKey });
#endif
        }
    }

    /// <summary>다음 Update에서 집중 대상을 공유하고 거리를 다시 판단하도록 예약합니다.</summary>
    private void HandleFocusedPlayerChanged(_PlayerNetworkSync player)
    {
        // 대상 참조는 Manager에서 읽으므로 여기서는 공유와 검사 시점만 갱신합니다.
        lastRequestedViewActor = -1;
        nextPropertyRetryTime = 0f;
        nextCheckTime = 0f;
    }

    /// <summary>집중 대상 공유와 실패 재시도, 주기적인 거리 판단을 진행합니다.</summary>
    private void Update()
    {
        if (syncManager == null || !PhotonNetwork.InRoom)
        {
            return;
        }

        _PlayerNetworkSync localPlayer = syncManager.LocalPlayerSync;
        _PlayerNetworkSync focusedPlayer = syncManager.FocusedPlayerSync;

        bool canPublishTarget = localPlayer != null && localPlayer.IsLocalControlActive && focusedPlayer != null && focusedPlayer.isActiveAndEnabled;
        int viewActor = canPublishTarget ? focusedPlayer.ActorNumber : 0;
        float currentTime = Time.unscaledTime;

        if (viewActor != lastRequestedViewActor && currentTime >= nextPropertyRetryTime)
        {
            bool requested = PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [c_viewActorKey] = viewActor });
            if (requested)
            {
                // 서버 적용 완료가 아니라 전송 요청 성공을 기록합니다.
                lastRequestedViewActor = viewActor;
                nextPropertyRetryTime = 0f;
            }
            else
            {
                nextPropertyRetryTime = currentTime + c_propertyRetryInterval;
            }
        }

        if (currentTime < nextCheckTime)
        {
            return;
        }

        nextCheckTime = currentTime + Mathf.Max(0.01f, checkInterval);
        UpdateSerializationRate(localPlayer);
    }

#if UNITY_EDITOR
    /// <summary>내 직렬화 빈도를 공유하고 모든 클라이언트의 보고값을 Console에 표시합니다.</summary>
    private void LateUpdate()
    {
        Room room = PhotonNetwork.CurrentRoom;
        Player localPhotonPlayer = PhotonNetwork.LocalPlayer;
        if (room == null || localPhotonPlayer == null)
        {
            return;
        }

        int currentRate = PhotonNetwork.SerializationRate;
        float currentTime = Time.unscaledTime;
        if (currentRate != lastRequestedDebugRate && currentTime >= nextDebugPropertyRetryTime)
        {
            bool requested = localPhotonPlayer.SetCustomProperties(new Hashtable { [c_debugRateKey] = currentRate });
            if (requested)
            {
                lastRequestedDebugRate = currentRate;
                nextDebugPropertyRetryTime = 0f;
            }
            else
            {
                nextDebugPropertyRetryTime = currentTime + c_propertyRetryInterval;
            }
        }

        debugRatesText.Clear();
        debugRatesText.Append("[PlayerSyncRateController] 직렬화 빈도(회/초): ");
        bool isFirstPlayer = true;
        foreach (Player player in room.Players.Values)
        {
            if (!isFirstPlayer)
            {
                debugRatesText.Append(" | ");
            }

            isFirstPlayer = false;
            debugRatesText.Append("Actor ").Append(player.ActorNumber);
            if (player.IsLocal)
            {
                debugRatesText.Append("(본인)");
            }

            debugRatesText.Append(": ");
            if (player.IsInactive)
            {
                debugRatesText.Append("비활성");
            }
            else if (player.IsLocal)
            {
                debugRatesText.Append(currentRate);
            }
            else if (player.CustomProperties[c_debugRateKey] is int reportedRate && reportedRate > 0)
            {
                // 원격 값은 다른 Editor 클라이언트가 마지막으로 공유한 설정값입니다.
                debugRatesText.Append(reportedRate);
            }
            else
            {
                debugRatesText.Append("미보고");
            }
        }

        Debug.Log(debugRatesText.ToString(), this);
    }
#endif

    /// <summary>다른 사용자의 집중 대상과의 거리로 빈도를 결정하고 한 번만 적용합니다.</summary>
    private void UpdateSerializationRate(_PlayerNetworkSync localPlayer)
    {
        bool shouldUseFarRate = false;
        if (localPlayer != null && localPlayer.IsLocalControlActive)
        {
            Vector3 senderPosition = localPlayer.transform.position;
            float threshold = isFar ? Mathf.Max(0f, nearDistance) : Mathf.Max(0f, nearDistance) + Mathf.Max(0f, distanceMargin);
            float thresholdSquared = threshold * threshold;
            bool hasOtherViewer = false;
            bool allViewersFar = true;

            foreach (Player viewer in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (viewer.IsLocal || viewer.IsInactive)
                {
                    continue;
                }

                hasOtherViewer = true;
                if (viewer.CustomProperties[c_viewActorKey] is not int viewActor || viewActor <= 0 || !syncManager.TryGetPlayer(viewActor, out _PlayerNetworkSync viewedPlayer) || !viewedPlayer.isActiveAndEnabled)
                {
                    allViewersFar = false;
                    break;
                }

                Vector3 difference = viewedPlayer.transform.position - senderPosition;
                if (difference.sqrMagnitude <= thresholdSquared)
                {
                    allViewersFar = false;
                    break;
                }
            }

            shouldUseFarRate = hasOtherViewer && allViewersFar;
        }

        isFar = shouldUseFarRate;
        int nearRate = Mathf.Max(1, nearSerializationRate);
        int targetRate = isFar ? Mathf.Max(1, Mathf.RoundToInt(nearRate * Mathf.Clamp(farRateMultiplier, 0.1f, 1f))) : nearRate;

        // 현재 클라이언트가 제어하는 모든 PhotonView의 직렬화 빈도에 적용됩니다.
        if (PhotonNetwork.SerializationRate != targetRate)
        {
            PhotonNetwork.SerializationRate = targetRate;
        }
    }
}
