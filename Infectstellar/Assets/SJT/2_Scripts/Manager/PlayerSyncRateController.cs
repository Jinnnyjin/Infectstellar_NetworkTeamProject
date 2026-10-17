// 추가 파일:
// C:/Users/82103/Desktop/UnityStudy/UnityProjects/Infectstellar_NetworkTeamProject/Infectstellar/Assets/SJT/2_Scripts/Manager/PlayerSyncRateController.cs
//
// 파일에 적용하지 않은 제안 코드이며 컴파일·통신 검증은 하지 않았습니다.
// 거리 15m, 완충 3m, 검사 간격 0.1초, 원거리 비율 50%는 미확정 예시입니다.
//
// 기존 SendRate·SerializationRate·PhotonView.Group·RPC·보간·외삽은 유지합니다.

using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>다른 사용자의 시청 대상과의 거리에 따라 로컬 플레이어의 스냅샷 송신 빈도를 조절합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerNetworkSyncManager), typeof(CinemachineCameraController))]
public sealed class PlayerSyncRateController : MonoBehaviourPunCallbacks
{
    private const string c_viewActorKey = "SJT.ViewActor";
    private const float c_propertyRetryInterval = 0.5f;

    [SerializeField, Min(0f), Tooltip("이 거리 안으로 들어오면 기존 송신 빈도를 유지합니다. 단위: m")]
    private float nearDistance = 15f;

    [SerializeField, Min(0f), Tooltip("원거리로 전환할 때 추가로 확보할 거리입니다. 단위: m")]
    private float distanceMargin = 3f;

    [SerializeField, Min(0.01f), Tooltip("거리별 송신 정책을 다시 판단하는 간격입니다. 단위: 초")]
    private float checkInterval = 0.1f;

    [SerializeField, Range(0.1f, 1f), Tooltip("원거리에서 사용할 기존 직렬화 빈도의 비율입니다.")]
    private float farRateMultiplier = 0.5f;

    private PlayerNetworkSyncManager syncManager;
    private CinemachineCameraController cameraController;
    private _PlayerNetworkSync localSync;

    private bool isFar;
    private float nextCheckTime;
    private float nextPropertyRetryTime;

    // 서버 캐시 반영 여부와 구분해, 전송 요청에 성공한 값을 보관합니다.
    private int lastRequestedViewActor = -1;

    private void Awake()
    {
        syncManager = GetComponent<PlayerNetworkSyncManager>();
        cameraController = GetComponent<CinemachineCameraController>();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        ResetController();

        if (PhotonNetwork.InRoom)
        {
            PublishViewActor(0);
        }
        else
        {
            ClearLocalViewProperty();
        }
    }

    public override void OnDisable()
    {
        RestoreLocalSendRate();

        if (PhotonNetwork.InRoom)
        {
            // 0은 현재 시청 기준을 알 수 없다는 뜻입니다.
            // 다른 송신자들은 이 값을 받으면 기존 빈도를 유지합니다.
            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable { [c_viewActorKey] = 0 });
        }
        else
        {
            ClearLocalViewProperty();
        }

        base.OnDisable();
    }

    public override void OnJoinedRoom()
    {
        ResetController();
        PublishViewActor(0);
    }

    public override void OnLeftRoom()
    {
        ResetController();
        ClearLocalViewProperty();
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        _PlayerNetworkSync registeredLocal = syncManager.LocalPlayerSync;

        if (localSync != registeredLocal)
        {
            RestoreLocalSendRate();

            localSync = registeredLocal;
            isFar = false;
            nextCheckTime = 0f;
        }

        bool localReady = localSync != null && localSync.IsLocalControlActive;

        int viewActor = localReady && cameraController.isActiveAndEnabled
            ? cameraController.CurrentViewActorNumber
            : 0;

        PublishViewActor(viewActor);

        if (!localReady)
        {
            RestoreLocalSendRate();
            return;
        }

        if (Time.unscaledTime < nextCheckTime)
        {
            return;
        }

        nextCheckTime = Time.unscaledTime + Mathf.Max(0.01f, checkInterval);

        UpdateSendRate();
    }

    /// <summary>다른 사용자가 보고 있는 플레이어와의 거리로 송신 빈도를 결정합니다.</summary>
    private void UpdateSendRate()
    {
        Vector3 senderPosition = localSync.transform.position;

        // 원거리 상태에서는 nearDistance 안으로 들어오면 기존 빈도로 복귀합니다.
        // 근거리 상태에서는 nearDistance + distanceMargin을 넘어야 원거리로 전환합니다.
        float threshold = isFar
            ? nearDistance
            : nearDistance + distanceMargin;

        float thresholdSquared = threshold * threshold;
        bool hasOtherViewer = false;

        foreach (Player viewer in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (viewer.IsLocal || viewer.IsInactive)
            {
                continue;
            }

            hasOtherViewer = true;

            // 시청 정보가 없거나 대상이 준비되지 않았으면 기존 빈도를 유지합니다.
            if (viewer.CustomProperties[c_viewActorKey] is not int viewActor
                || viewActor <= 0
                || !syncManager.TryGetPlayer(viewActor, out _PlayerNetworkSync viewedPlayer))
            {
                UseOriginalSendRate();
                return;
            }

            Vector3 difference = viewedPlayer.transform.position - senderPosition;

            // 한 명이라도 가까운 곳을 보고 있으면 모든 수신자에게 기존 빈도로 보냅니다.
            if (difference.sqrMagnitude <= thresholdSquared)
            {
                UseOriginalSendRate();
                return;
            }
        }

        if (!hasOtherViewer)
        {
            UseOriginalSendRate();
            return;
        }

        isFar = true;

        float targetRate = PhotonNetwork.SerializationRate
            * Mathf.Clamp(farRateMultiplier, 0.1f, 1f);

        localSync.SetSnapshotSendRate(targetRate);
    }

    /// <summary>거리별 제한을 해제하고 기존 송신 빈도를 유지합니다.</summary>
    private void UseOriginalSendRate()
    {
        isFar = false;
        localSync.SetSnapshotSendRate(0f);
    }

    /// <summary>현재 보는 플레이어가 바뀌었을 때만 해당 번호를 공유합니다.</summary>
    private void PublishViewActor(int actorNumber)
    {
        if (actorNumber == lastRequestedViewActor
            || Time.unscaledTime < nextPropertyRetryTime)
        {
            return;
        }

        bool requested = PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { [c_viewActorKey] = actorNumber });

        if (requested)
        {
            // true는 서버 적용 완료가 아니라 전송 요청 성공을 뜻합니다.
            lastRequestedViewActor = actorNumber;
            nextPropertyRetryTime = 0f;
        }
        else
        {
            nextPropertyRetryTime = Time.unscaledTime + c_propertyRetryInterval;
        }
    }

    /// <summary>이전에 관리하던 플레이어의 송신 제한을 해제합니다.</summary>
    private void RestoreLocalSendRate()
    {
        if (localSync != null)
        {
            localSync.SetSnapshotSendRate(0f);
        }

        localSync = null;
    }

    /// <summary>방 입퇴장과 재활성화 시 이전 정책 상태를 초기화합니다.</summary>
    private void ResetController()
    {
        RestoreLocalSendRate();

        isFar = false;
        nextCheckTime = 0f;
        nextPropertyRetryTime = 0f;
        lastRequestedViewActor = -1;
    }

    /// <summary>다음 방에 이전 시청 대상이 남지 않도록 로컬 전용 속성을 제거합니다.</summary>
    private void ClearLocalViewProperty()
    {
        if (PhotonNetwork.LocalPlayer != null)
        {
            PhotonNetwork.RemovePlayerCustomProperties(
                new[] { c_viewActorKey });
        }
    }
}