using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>PUN 생성 초기화, 확정값 RPC와 시간 기록 스냅샷의 송수신을 담당합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView), typeof(PlayerSnapshotPlayback))]
public sealed class PlayerNetworkSync : MonoBehaviour, IPunObservable, IPunInstantiateMagicCallback
{
    [SerializeField] private GameObject playerCamera;

    private PhotonView view;
    private PlayerController controller;
    private PlayerStateMachine stateMachine;
    private PlayerMotor motor;
    private PlayerLook playerLook;
    private FlashlightController flashlight;
    private PlayerAnimationController animationController;
    private PlayerSnapshotPlayback playback;
    private PlayerSyncRateManager manager;
    private Transform body;
    private bool isInitialized;
    private bool isLocal;
    private bool isControlSuspended;
    private int spawnSlot = -1;
    private int actorNumber;
    private int rate = 30;
    private int sequence;
    private int lifeVersion;
    private double nextSendTime;
    private double nextInitialRequestTime;
    private Vector3 latestPosition;
    private bool hasLatestPosition;
    private double latestPositionTime = double.NegativeInfinity;

#if UNITY_EDITOR
    [SerializeField] private int sentSnapshots;
    [SerializeField] private int receivedSnapshots;
    [SerializeField] private int currentRate = 30;
#endif

    /// <summary>생성 콜백과 관리자 등록이 완료됐는지 반환합니다.</summary>
    public bool IsInitialized => isInitialized;
    /// <summary>이번 인스턴스가 담당하던 제어 권한이 유효할 때만 로컬 조작을 허용합니다.</summary>
    public bool IsLocalControlActive => isInitialized && isLocal && !isControlSuspended && manager != null && manager.isActiveAndEnabled && PhotonNetwork.InRoom && view.IsMine && view.ControllerActorNr == actorNumber;
    /// <summary>생성 데이터에 저장된 시작 위치 슬롯입니다.</summary>
    public int SpawnSlot => spawnSlot;
    /// <summary>생성한 플레이어의 ActorNumber입니다.</summary>
    public int ActorNumber => actorNumber;
    /// <summary>관리자 거리 계산용 최신 수신 위치가 있는지 반환합니다.</summary>
    public bool HasLatestPosition => hasLatestPosition;
    /// <summary>표시 지연과 독립적인 최신 수신 위치입니다.</summary>
    public Vector3 LatestPosition => latestPosition;
    /// <summary>소유자 거리 계산용 현재 위치입니다.</summary>
    public Vector3 Position => body.position;
    /// <summary>이 플레이어만 사용하는 자식 Cinemachine 카메라 객체입니다.</summary>
    public GameObject PlayerCamera => playerCamera;

    /// <summary>관리자의 고정·적응형 버퍼 선택을 원격 재생에 전달합니다.</summary>
    public bool UseAdaptiveBuffer => manager != null && manager.UseAdaptiveBuffer;

    /// <summary>관리자 종료와 로컬 교체 때 기존 입력을 먼저 해제합니다.</summary>
    public void SuspendLocalControl()
    {
        isControlSuspended = true;
        if (controller != null)
        {
            controller.SetLocalControlActive(false);
        }
    }

    /// <summary>역할 확정 전에는 입력과 카메라를 열지 않고 참조만 보관합니다.</summary>
    private void Awake()
    {
        body = transform;
        view = GetComponent<PhotonView>();
        controller = GetComponent<PlayerController>();
        stateMachine = GetComponent<PlayerStateMachine>();
        motor = GetComponent<PlayerMotor>();
        playerLook = GetComponent<PlayerLook>();
        flashlight = GetComponent<FlashlightController>();
        animationController = GetComponentInChildren<PlayerAnimationController>(true);
        playback = GetComponent<PlayerSnapshotPlayback>();
        PlayerInput input = GetComponent<PlayerInput>();
        if (input != null)
        {
            input.enabled = false;
        }
        if (playerCamera != null)
        {
            playerCamera.SetActive(false);
        }
    }

    /// <summary>초기 RPC 응답이 아직 없으면 제어자에게 제한된 간격으로 다시 요청합니다.</summary>
    private void Update()
    {
        if (isInitialized && !isLocal && !playback.HasInitialState && PhotonNetwork.InRoom && PhotonNetwork.Time >= nextInitialRequestTime)
        {
            RequestInitialState();
        }
    }

    /// <summary>객체 수명이 끝나면 구독과 관리자 등록을 해제합니다.</summary>
    private void OnDestroy()
    {
        if (flashlight != null)
        {
            flashlight.D_FlashlightStateChanged -= HandleFlashlightChanged;
        }
        if (animationController != null)
        {
            animationController.D_HandsUpStateChanged -= HandleHandsUpChanged;
        }
        if (stateMachine != null)
        {
            stateMachine.StateChanged -= HandleStateChanged;
        }
        if (manager != null)
        {
            manager.Unregister(this);
        }
    }

    /// <summary>로컬·원격 복제 모두 생성 정보를 확인하고 같은 활성 씬의 관리자를 한 번 연결합니다.</summary>
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = view.InstantiationData;
        if (data == null || data.Length != 1 || !(data[0] is int slot) || slot < 0 || slot >= 4 || info.Sender == null || info.Sender.ActorNumber != view.OwnerActorNr)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerNetworkSync] 생성 데이터 또는 생성자가 올바르지 않습니다.", this);
#endif
            return;
        }

        spawnSlot = slot;
        actorNumber = info.Sender.ActorNumber;
        PlayerSyncRateManager foundManager = null;
        foreach (PlayerSyncRateManager candidate in FindObjectsByType<PlayerSyncRateManager>(FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == gameObject.scene)
            {
                if (foundManager != null)
                {
#if UNITY_EDITOR
                    Debug.LogError("[PlayerNetworkSync] 같은 씬에 관리자가 두 개 이상 있습니다.", this);
#endif
                    return;
                }
                foundManager = candidate;
            }
        }
        Initialize(foundManager);
    }

    /// <summary>의존성과 역할을 확정하고 관리자 등록 후 로컬 제어 또는 원격 재생을 시작합니다.</summary>
    public void Initialize(PlayerSyncRateManager manager)
    {
        if (isInitialized)
        {
            return;
        }
        if (manager == null || !manager.isActiveAndEnabled || controller == null || !controller.IsInitialized || !controller.isActiveAndEnabled || stateMachine == null || !stateMachine.isActiveAndEnabled || motor == null || !motor.CanSimulate || playerLook == null || !playerLook.isActiveAndEnabled || flashlight == null || !flashlight.isActiveAndEnabled || animationController == null || !animationController.isActiveAndEnabled || playback == null || !playback.isActiveAndEnabled || playerCamera == null || !PhotonNetwork.InRoom || view.ViewID == 0 || spawnSlot < 0)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerNetworkSync] 네트워크 플레이어의 필수 참조 또는 씬 관리자가 없습니다. 조작을 시작하지 않습니다.", this);
#endif
            return;
        }
        this.manager = manager;
        isLocal = view.IsMine;
        isControlSuspended = false;
        playback.Initialize(this, stateMachine, motor, playerLook, flashlight, animationController, !isLocal);
        isInitialized = true;
        if (!manager.Register(this))
        {
            isInitialized = false;
            return;
        }
        flashlight.D_FlashlightStateChanged += HandleFlashlightChanged;
        animationController.D_HandsUpStateChanged += HandleHandsUpChanged;
        stateMachine.StateChanged += HandleStateChanged;
        controller.SetLocalControlActive(isLocal);
        if (!isLocal)
        {
            RequestInitialState();
        }
    }

    /// <summary>높은 전송 등급으로 바뀌면 다음 PUN 관찰 호출에서 즉시 최신 기록을 보냅니다.</summary>
    public void SetSendRate(int value)
    {
        value = value >= 30 ? 30 : value >= 15 ? 15 : 10;
        if (value > rate)
        {
            nextSendTime = 0d;
        }
        rate = value;
#if UNITY_EDITOR
        currentRate = rate;
#endif
    }

    /// <summary>기본 지원 타입만 정해진 순서대로 쓰거나 읽으며 개별 전송 시각이 아니면 송신을 생략합니다.</summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (!IsLocalControlActive)
            {
                return;
            }
            double now = PhotonNetwork.Time;
            double interval = 1d / rate;
            if (now + 0.001d < nextSendTime)
            {
                return;
            }
            if (nextSendTime <= 0d || now - nextSendTime > interval)
            {
                nextSendTime = now;
            }
            nextSendTime += interval;
            PlayerSnapshot snapshot = CaptureSnapshot(now);
            stream.SendNext(snapshot.Time);
            stream.SendNext(snapshot.Position);
            stream.SendNext(snapshot.Velocity);
            stream.SendNext(snapshot.Yaw);
            stream.SendNext(snapshot.Pitch);
            stream.SendNext((int)snapshot.State);
            stream.SendNext(snapshot.Direction);
            stream.SendNext(snapshot.Crouching);
            stream.SendNext(snapshot.Rate);
            stream.SendNext(snapshot.LifeVersion);
#if UNITY_EDITOR
            sentSnapshots++;
#endif
            return;
        }

        var received = new PlayerSnapshot((double)stream.ReceiveNext(), (Vector3)stream.ReceiveNext(), (Vector3)stream.ReceiveNext(), (float)stream.ReceiveNext(), (float)stream.ReceiveNext(), (PlayerState)(int)stream.ReceiveNext(), (Vector2)stream.ReceiveNext(), (bool)stream.ReceiveNext(), (int)stream.ReceiveNext(), (int)stream.ReceiveNext());
        if (!CanReceive(info) || !IsValidSnapshot(received))
        {
            return;
        }
        if (playback.PushSnapshot(received, PhotonNetwork.Time))
        {
            latestPosition = received.Position;
            latestPositionTime = received.Time;
            hasLatestPosition = true;
        }
#if UNITY_EDITOR
        receivedSnapshots++;
#endif
    }

    /// <summary>현재 로컬 상태를 하나의 확정된 시간 기록으로 수집합니다.</summary>
    private PlayerSnapshot CaptureSnapshot(double time) => new PlayerSnapshot(time, body.position, motor.Velocity, body.eulerAngles.y, playerLook.PitchDegrees, stateMachine.CurrentState, controller.MoveDirection, motor.IsCrouching, rate, lifeVersion);

    /// <summary>수신 기록의 타입 범위와 비정상 수치를 검사합니다.</summary>
    private static bool IsValidSnapshot(PlayerSnapshot snapshot)
    {
        return !double.IsNaN(snapshot.Time) && !double.IsInfinity(snapshot.Time) && snapshot.LifeVersion >= 0 && snapshot.State >= PlayerState.Idle && snapshot.State <= PlayerState.Dead && (snapshot.Rate == 10 || snapshot.Rate == 15 || snapshot.Rate == 30)
            && IsFinite(snapshot.Position.x) && IsFinite(snapshot.Position.y) && IsFinite(snapshot.Position.z) && IsFinite(snapshot.Velocity.x) && IsFinite(snapshot.Velocity.y) && IsFinite(snapshot.Velocity.z) && IsFinite(snapshot.Yaw) && IsFinite(snapshot.Pitch) && IsFinite(snapshot.Direction.x) && IsFinite(snapshot.Direction.y);
    }

    /// <summary>벡터와 각도에 NaN 또는 무한대가 들어오지 않게 합니다.</summary>
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>현재 제어자에게서 온 원격 기록만 받습니다.</summary>
    private bool CanReceive(PhotonMessageInfo info) => isInitialized && !isLocal && PhotonNetwork.InRoom && info.Sender != null && info.Sender.ActorNumber == view.ControllerActorNr;

    /// <summary>초기 응답이 유실되거나 제어자 초기화보다 먼저 도착해도 다시 요청합니다.</summary>
    private void RequestInitialState()
    {
        nextInitialRequestTime = PhotonNetwork.Time + 1d;
        Player target = view.Controller;
        if (target != null)
        {
            view.RPC(nameof(RpcRequestInitialState), target);
        }
    }

    /// <summary>요청자를 확인하고 현재 전체 상태를 그 클라이언트에게만 제공합니다.</summary>
    [PunRPC]
    private void RpcRequestInitialState(PhotonMessageInfo info)
    {
        if (!IsLocalControlActive || info.Sender == null || info.Sender.ActorNumber == actorNumber)
        {
            return;
        }
        PlayerSnapshot s = CaptureSnapshot(PhotonNetwork.Time);
        view.RPC(nameof(RpcProvideInitialState), info.Sender, s.Time, s.Position, s.Velocity, s.Yaw, s.Pitch, (int)s.State, s.Direction, s.Crouching, s.Rate, s.LifeVersion, flashlight.IsOn, animationController.IsHandsUp, sequence);
    }

    /// <summary>최초 전체 상태를 재생 기준과 이벤트 순서 하한으로 사용합니다.</summary>
    [PunRPC]
    private void RpcProvideInitialState(double time, Vector3 position, Vector3 velocity, float yaw, float pitch, int state, Vector2 direction, bool crouching, int rate, int version, bool light, bool handsUp, int changeSequence, PhotonMessageInfo info)
    {
        var snapshot = new PlayerSnapshot(time, position, velocity, yaw, pitch, (PlayerState)state, direction, crouching, rate, version);
        if (!CanReceive(info) || !IsValidSnapshot(snapshot) || changeSequence < 0 || playback.HasInitialState)
        {
            return;
        }
        if (snapshot.Time >= latestPositionTime)
        {
            latestPosition = snapshot.Position;
            latestPositionTime = snapshot.Time;
        }
        hasLatestPosition = true;
        playback.SetInitialState(snapshot, light, handsUp, changeSequence, PhotonNetwork.Time);
    }

    /// <summary>로컬에서 적용된 손전등의 확정값을 다른 클라이언트에 전달합니다.</summary>
    private void HandleFlashlightChanged(bool value)
    {
        if (IsLocalControlActive)
        {
            view.RPC(nameof(RpcSetFlashlight), RpcTarget.Others, value, PhotonNetwork.Time, ++sequence, lifeVersion);
        }
    }

    /// <summary>기존 HandsUp 호출의 확정값을 다른 클라이언트에 전달합니다.</summary>
    private void HandleHandsUpChanged(bool value)
    {
        if (IsLocalControlActive)
        {
            view.RPC(nameof(RpcSetHandsUp), RpcTarget.Others, value, PhotonNetwork.Time, ++sequence, lifeVersion);
        }
    }

    /// <summary>사망과 부활만 생사 버전을 증가시켜 전달합니다.</summary>
    private void HandleStateChanged(PlayerState previous, PlayerState current)
    {
        if (IsLocalControlActive && (previous == PlayerState.Dead) != (current == PlayerState.Dead))
        {
            view.RPC(nameof(RpcSetLifeState), RpcTarget.Others, current != PlayerState.Dead, PhotonNetwork.Time, ++sequence, ++lifeVersion);
        }
    }

    /// <summary>손전등 RPC를 화면 재생 대기열에 넣습니다.</summary>
    [PunRPC]
    private void RpcSetFlashlight(bool value, double time, int order, int version, PhotonMessageInfo info) => QueueEvent(PlayerStateEventType.Flashlight, value, time, order, version, info);
    /// <summary>HandsUp RPC를 화면 재생 대기열에 넣습니다.</summary>
    [PunRPC]
    private void RpcSetHandsUp(bool value, double time, int order, int version, PhotonMessageInfo info) => QueueEvent(PlayerStateEventType.HandsUp, value, time, order, version, info);
    /// <summary>생사 RPC를 화면 재생 대기열에 넣습니다.</summary>
    [PunRPC]
    private void RpcSetLifeState(bool value, double time, int order, int version, PhotonMessageInfo info) => QueueEvent(PlayerStateEventType.LifeState, value, time, order, version, info);

    /// <summary>원격 적용에 의해 이벤트가 발생해도 재송신하지 않고 제어자 메시지만 저장합니다.</summary>
    private void QueueEvent(PlayerStateEventType type, bool value, double time, int order, int version, PhotonMessageInfo info)
    {
        if (CanReceive(info) && order > 0 && version >= 0 && !double.IsNaN(time) && !double.IsInfinity(time))
        {
            playback.PushEvent(new PlayerStateEvent(type, value, time, order, version));
        }
    }
}
