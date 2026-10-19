using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>씬 몬스터의 Master 권한·이동 기록·피해 전파와 제거를 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView), typeof(MonsterController))]
public sealed class MonsterNetworkSync : MonoBehaviourPunCallbacks, IPunObservable
{
    [SerializeField, Min(0f), Tooltip("원격 움직임을 늦춰 재생할 시간(초)")] private float bufferDelay = 0.1f;

    private PhotonView view;
    private MonsterController controller;
    private Health health;
    private Transform cachedTransform;
    private GameObject cachedObject;
    private MonsterSnapshotPlayback playback;
    private MonsterSnapshot? latest;
    private bool initialized;
    private bool running;
    private bool isAuthority;

    /// <summary>현재 룸에서 이 클라이언트가 몬스터 행동을 결정하는지 나타냅니다.</summary>
    public bool IsAuthorityActive => running && isAuthority && PhotonNetwork.InRoom;

    /// <summary>필수 참조와 이 몬스터의 재생 버퍼를 준비합니다.</summary>
    private void Awake()
    {
        view = GetComponent<PhotonView>();
        controller = GetComponent<MonsterController>();
        health = GetComponent<Health>();
        cachedTransform = transform;
        cachedObject = gameObject;
        playback = new MonsterSnapshotPlayback(bufferDelay);
    }

    /// <summary>PUN 콜백과 로컬 피해 송신 알림을 구독합니다.</summary>
    public override void OnEnable()
    {
        base.OnEnable();
        health.D_DamageApplied += HandleDamageApplied;
        if (initialized)
        {
            StartNetwork();
        }
    }

    /// <summary>몬스터 FSM 준비가 끝난 뒤 게임 씬의 현재 룸 권한을 연결합니다.</summary>
    internal void Initialize()
    {
        initialized = true;
        StartNetwork();
    }

    /// <summary>게임 시작 전에 참가한 룸과 씬 ViewID가 유효할 때만 실행합니다.</summary>
    private void StartNetwork()
    {
        if (!isActiveAndEnabled || !PhotonNetwork.InRoom || view.ViewID == 0)
        {
            StopNetwork();
            return;
        }
        running = true;
        RefreshAuthority(PhotonNetwork.IsMasterClient);
    }

    /// <summary>Master는 제거 시간을 확인하고 나머지는 원격 자세를 재생합니다.</summary>
    private void Update()
    {
        if (!running)
        {
            return;
        }
        if (!PhotonNetwork.InRoom)
        {
            StopNetwork();
            return;
        }
        if (isAuthority)
        {
            if (controller.CurrentState == MonsterState.Dead && controller.StateTimeRemaining <= 0f && view.IsMine)
            {
                PhotonNetwork.Destroy(cachedObject);
            }
            return;
        }
        MonsterSnapshot? sampled = playback.GetPlaybackSnapshot(PhotonNetwork.Time);
        if (!sampled.HasValue)
        {
            return;
        }
        MonsterSnapshot snapshot = sampled.Value;
        // 로컬 피해로 먼저 죽었으면 오래된 생존 기록으로 시체를 움직이지 않습니다.
        if (controller.CurrentState == MonsterState.Dead && snapshot.State != MonsterState.Dead)
        {
            return;
        }
        cachedTransform.SetPositionAndRotation(snapshot.Position, Quaternion.Euler(0f, snapshot.YRotation, 0f));
        controller.ApplyRemoteState(snapshot.State, Remaining(snapshot.StateEndsAt));
    }

    /// <summary>Master의 자세·행동을 보내고 현재 Master의 새 기록만 받습니다.</summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (!IsAuthorityActive)
            {
                return;
            }
            double now = PhotonNetwork.Time;
            stream.SendNext(now);
            stream.SendNext(cachedTransform.position);
            stream.SendNext(cachedTransform.eulerAngles.y);
            stream.SendNext(controller.Velocity);
            stream.SendNext((int)controller.CurrentState);
            stream.SendNext(now + controller.StateTimeRemaining);
            return;
        }
        MonsterSnapshot snapshot = new MonsterSnapshot((double)stream.ReceiveNext(), (Vector3)stream.ReceiveNext(),
            (float)stream.ReceiveNext(), (Vector3)stream.ReceiveNext(), (MonsterState)(int)stream.ReceiveNext(), (double)stream.ReceiveNext());
        if (!running || isAuthority || info.Sender == null || PhotonNetwork.MasterClient == null ||
            info.Sender.ActorNumber != PhotonNetwork.MasterClient.ActorNumber ||
            double.IsNaN(snapshot.Time) || double.IsInfinity(snapshot.Time) ||
            double.IsNaN(snapshot.StateEndsAt) || double.IsInfinity(snapshot.StateEndsAt) ||
            (latest.HasValue && snapshot.Time <= latest.Value.Time))
        {
            return;
        }
        latest = snapshot;
        playback.PushSnapshot(snapshot);
    }

    /// <summary>로컬 피해는 소유권과 관계없이 한 번만 다른 참가자에게 보냅니다.</summary>
    private void HandleDamageApplied(DamageInfo damageInfo)
    {
        if (running && PhotonNetwork.InRoom && view.ViewID != 0)
        {
            view.RPC(nameof(RpcTakeDamage), RpcTarget.Others, damageInfo.Amount);
        }
    }

    /// <summary>피해를 재송신 없이 적용하고 Master의 경직 판단에 연결합니다.</summary>
    [PunRPC]
    private void RpcTakeDamage(float amount, PhotonMessageInfo info)
    {
        if (!running || !PhotonNetwork.InRoom || info.Sender == null)
        {
            return;
        }
        if (health.ApplyDamage(new DamageInfo(amount)))
        {
            controller.NotifyDamageApplied();
        }
    }

    /// <summary>새 Master를 콜백 인자로 판단해 PhotonView 갱신 순서에 의존하지 않습니다.</summary>
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (running)
        {
            RefreshAuthority(newMasterClient.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber);
        }
    }

    /// <summary>지연 재생 전의 최신 행동을 승계하고 이전 송신자 버퍼를 폐기합니다.</summary>
    private void RefreshAuthority(bool active)
    {
        if (!isAuthority && latest.HasValue)
        {
            MonsterSnapshot snapshot = latest.Value;
            controller.ApplyRemoteState(snapshot.State, Remaining(snapshot.StateEndsAt));
            if (active)
            {
                // 코너 보간·외삽 위치 대신 이전 Master의 실제 NavMesh 위치에서 승계합니다.
                cachedTransform.SetPositionAndRotation(snapshot.Position, Quaternion.Euler(0f, snapshot.YRotation, 0f));
            }
        }
        playback.Clear();
        latest = null;
        isAuthority = active;
        controller.SetSimulationAuthority(active);
    }

    /// <summary>네트워크 종료 시 씬에 남은 몬스터를 정지합니다.</summary>
    public override void OnLeftRoom()
    {
        StopNetwork();
    }

    /// <summary>연결이 끊긴 뒤 단독 AI로 전환하지 않습니다.</summary>
    public override void OnDisconnected(DisconnectCause cause)
    {
        StopNetwork();
    }

    /// <summary>권한과 이전 재생 기록을 정리합니다.</summary>
    private void StopNetwork()
    {
        running = false;
        isAuthority = false;
        playback.Clear();
        latest = null;
        controller.SetSimulationAuthority(false);
    }

    /// <summary>비활성화 시 피해 알림·PUN 콜백을 해제하고 이동을 멈춥니다.</summary>
    public override void OnDisable()
    {
        health.D_DamageApplied -= HandleDamageApplied;
        StopNetwork();
        base.OnDisable();
    }

    /// <summary>네트워크 종료 시각을 현재 프레임의 잔여 시간으로 바꿉니다.</summary>
    private static float Remaining(double endsAt)
    {
        return Mathf.Max(0f, (float)(endsAt - PhotonNetwork.Time));
    }
}
