using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>PUN 생성 초기화, 확정값 RPC와 시간 기록 스냅샷의 송수신을 담당합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView), typeof(PlayerSnapshotPlayback))]
public sealed class PlayerNetworkSync : MonoBehaviour, IPunObservable, IPunInstantiateMagicCallback
{
    [SerializeField] private GameObject playerCamera; // 내가 조종할 때 켜는 플레이어의 자식 카메라

    private PhotonView view; // 이 플레이어의 조작 권한 확인과 RPC 전송에 쓰는 PUN 컴포넌트
    private PlayerController controller; // 로컬 조작을 켜고 끄며 이동 입력을 읽을 컴포넌트
    private PlayerStateMachine stateMachine; // 걷기·점프·사망 등 현재 상태를 읽을 컴포넌트
    private PlayerMotor motor; // 실제 이동 속도와 웅크림 여부를 읽을 컴포넌트
    private PlayerLook playerLook; // 위아래 시선 각도를 읽을 컴포넌트
    private FlashlightController flashlight; // 손전등 상태를 읽고 변경을 감지할 컴포넌트
    private PlayerAnimationController animationController; // 이동 방향·손들기 애니메이션을 관리하는 컴포넌트
    private PlayerSnapshotPlayback playback; // 받은 이동 기록과 RPC를 시간에 맞춰 화면에 적용할 컴포넌트
    private PlayerSyncRateManager manager; // 플레이어 등록·카메라·거리별 전송 빈도를 관리하는 씬 컴포넌트
    private Transform body; // 전송할 플레이어의 위치와 몸 회전을 읽을 Transform
    private bool isInitialized; // 필수 참조 연결과 관리자 등록이 완료됐는지 여부
    private bool isLocal; // 초기화 당시 내가 조종하는 플레이어였는지 여부
    private bool isControlSuspended; // 관리자 종료나 플레이어 교체로 로컬 조작이 중단됐는지 여부
    private int spawnSlot = -1; // 생성할 때 배정받은 시작 위치 번호(0~3, -1이면 미배정)
    private int actorNumber; // 이 플레이어를 생성한 클라이언트의 방 안 번호
    private int rate = 30; // 현재 초당 스냅샷 전송 목표 횟수(30/15/10Hz)
    private int sequence; // RPC 상태 변경마다 증가하는 순서 번호
    private int lifeVersion; // 사망·부활마다 증가해 이전 생사 상태를 구분하는 번호
    private double nextSendTime; // 다음 스냅샷을 보낼 Photon 시각(초)
    private double nextInitialRequestTime; // 초기 전체 상태를 다시 요청할 Photon 시각(초)
    private Vector3 latestPosition; // 거리 계산에 쓰는 상대의 최신 수신 위치
    private bool hasLatestPosition; // 거리 계산에 사용할 위치를 한 번 이상 받았는지 여부
    private double latestPositionTime = double.NegativeInfinity; // 최신 수신 위치가 상대에게서 기록된 Photon 시각(초)

#if UNITY_EDITOR
    [SerializeField] private int sentSnapshots; // 이 플레이어가 보낸 스냅샷의 누적 개수
    [SerializeField] private int receivedSnapshots; // 제어자·수치 검사를 통과해 받은 스냅샷의 누적 개수
    [SerializeField] private int currentRate = 30; // Inspector에서 확인할 현재 전송 목표 횟수(Hz)
#endif

    /// <summary>생성 콜백과 관리자 등록이 완료됐는지 반환합니다.</summary>
    public bool IsInitialized => isInitialized;
    /// <summary>이번 인스턴스가 담당하던 제어 권한이 유효할 때만 로컬 조작을 허용합니다.</summary>
    public bool IsLocalControlActive
    {
        get
        {
            if (!isInitialized || !isLocal || isControlSuspended)
            {
                return false;
            }
            if (manager == null || !manager.isActiveAndEnabled || !PhotonNetwork.InRoom)
            {
                return false;
            }
            return view.IsMine && view.ControllerActorNr == actorNumber;
        }
    }
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

    /// <summary>로컬·원격 복제 모두 생성 정보를 확인하고 씬 싱글톤 관리자를 연결합니다.</summary>
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
        Initialize(PlayerSyncRateManager.Instance);
    }

    /// <summary>의존성과 역할을 확정하고 관리자 등록 후 로컬 제어 또는 원격 재생을 시작합니다.</summary>
    public void Initialize(PlayerSyncRateManager manager)
    {
        if (isInitialized)
        {
            return;
        }
        if (!CanInitialize(manager))
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

    /// <summary>관리자·이동·표시·PUN 생성 준비를 차례로 확인합니다.</summary>
    private bool CanInitialize(PlayerSyncRateManager manager)
    {
        if (manager == null || !manager.isActiveAndEnabled || playerCamera == null)
        {
            return false;
        }
        if (manager.gameObject.scene != gameObject.scene)
        {
            return false;
        }
        if (controller == null || !controller.IsInitialized || !controller.isActiveAndEnabled)
        {
            return false;
        }
        if (stateMachine == null || !stateMachine.isActiveAndEnabled || motor == null || !motor.CanSimulate)
        {
            return false;
        }
        if (playerLook == null || !playerLook.isActiveAndEnabled || flashlight == null || !flashlight.isActiveAndEnabled)
        {
            return false;
        }
        if (animationController == null || !animationController.isActiveAndEnabled || playback == null || !playback.isActiveAndEnabled)
        {
            return false;
        }
        return PhotonNetwork.InRoom && view.ViewID != 0 && spawnSlot >= 0;
    }

    /// <summary>높은 전송 등급으로 바뀌면 다음 PUN 관찰 호출에서 즉시 최신 기록을 보냅니다.</summary>
    public void SetSendRate(int value)
    {
        if (value >= 30)
        {
            value = 30;
        }
        else if (value >= 15)
        {
            value = 15;
        }
        else
        {
            value = 10;
        }
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
            SendSnapshot(stream);
        }
        else
        {
            ReceiveSnapshot(stream, info);
        }
    }

    /// <summary>로컬 제어와 전송 시각을 확인한 뒤 현재 상태를 보냅니다.</summary>
    private void SendSnapshot(PhotonStream stream)
    {
        if (!IsLocalControlActive)
        {
            return;
        }

        double now = PhotonNetwork.Time;
        if (!AdvanceSendSchedule(now))
        {
            return;
        }

        PlayerSnapshot snapshot = CaptureSnapshot(now);
        snapshot.WriteTo(stream);
#if UNITY_EDITOR
        sentSnapshots++;
#endif
    }

    /// <summary>PUN 관찰 호출 중 개별 등급의 전송 시각에 도달한 호출만 통과시킵니다.</summary>
    private bool AdvanceSendSchedule(double now)
    {
        double interval = 1d / rate;
        if (now + 0.001d < nextSendTime)
        {
            return false;
        }
        if (nextSendTime <= 0d || now - nextSendTime > interval)
        {
            nextSendTime = now;
        }
        nextSendTime += interval;
        return true;
    }

    /// <summary>전송 형식을 읽고 제어자와 수치를 검사한 뒤 재생 버퍼에 넣습니다.</summary>
    private void ReceiveSnapshot(PhotonStream stream, PhotonMessageInfo info)
    {
        PlayerSnapshot received = PlayerSnapshot.ReadFrom(stream);
        if (!CanReceive(info) || !received.IsValid())
        {
            return;
        }
        if (playback.PushSnapshot(received, PhotonNetwork.Time))
        {
            RecordLatestPosition(received);
        }
#if UNITY_EDITOR
        receivedSnapshots++;
#endif
    }

    /// <summary>현재 로컬 상태를 하나의 확정된 시간 기록으로 수집합니다.</summary>
    private PlayerSnapshot CaptureSnapshot(double time)
    {
        return new PlayerSnapshot(
            time, body.position, motor.Velocity,
            body.eulerAngles.y, playerLook.PitchDegrees,
            stateMachine.CurrentState, controller.MoveDirection,
            motor.IsCrouching, rate, lifeVersion);
    }

    /// <summary>거리 계산에 사용할 최신 위치와 그 시각을 함께 보관합니다.</summary>
    private void RecordLatestPosition(PlayerSnapshot snapshot)
    {
        latestPosition = snapshot.Position;
        latestPositionTime = snapshot.Time;
        hasLatestPosition = true;
    }

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
        PlayerSnapshot snapshot = CaptureSnapshot(PhotonNetwork.Time);
        view.RPC(nameof(RpcProvideInitialState), info.Sender,
            snapshot.Time, snapshot.Position, snapshot.Velocity,
            snapshot.Yaw, snapshot.Pitch, (int)snapshot.State,
            snapshot.Direction, snapshot.Crouching, snapshot.Rate, snapshot.LifeVersion,
            flashlight.IsOn, animationController.IsHandsUp, sequence);
    }

    /// <summary>최초 전체 상태를 재생 기준과 이벤트 순서 하한으로 사용합니다.</summary>
    [PunRPC]
    private void RpcProvideInitialState(double time, Vector3 position, Vector3 velocity, float yaw, float pitch, int state, Vector2 direction, bool crouching, int rate, int version, bool light, bool handsUp, int changeSequence, PhotonMessageInfo info)
    {
        var snapshot = new PlayerSnapshot(time, position, velocity, yaw, pitch,
            (PlayerState)state, direction, crouching, rate, version);
        if (!CanReceive(info) || !snapshot.IsValid() || changeSequence < 0 || playback.HasInitialState)
        {
            return;
        }
        if (snapshot.Time >= latestPositionTime)
        {
            RecordLatestPosition(snapshot);
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
