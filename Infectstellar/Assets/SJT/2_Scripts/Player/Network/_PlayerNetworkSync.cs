using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>PUN 생성·등록·스냅샷 송수신을 연결하고 보간된 원격 자세를 적용합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView))]
public sealed class _PlayerNetworkSync : MonoBehaviour, IPunObservable, IPunInstantiateMagicCallback
{
    [SerializeField, Min(0f)] private float bufferDelay = 0.1f; // 다음 기록이 도착할 여유를 주려고 상대 움직임을 늦춰 보여주는 시간(초)
    [SerializeField] private GameObject playerCamera; // 내 플레이어만 켤 자식 Cinemachine 카메라
    [SerializeField] private PlayerLook playerLook; // 상하 시선 적용 대상
    [SerializeField] private PlayerMotor playerMotor; // 캡슐 웅크림 적용 대상
    [SerializeField] private Transform body; // 위치·몸 회전 적용 대상
    [SerializeField, Tooltip("StateMachine에 연결한 것과 같은 모델의 애니메이션 컴포넌트")]
    private PlayerAnimationController animationController; // 방향·손들기 애니메이션
    [SerializeField] private GameObject mineModel; // 로컬에서 숨길 모델; 지정하지 않으면 유지

    private PhotonView photonView; // 권한 검사와 RPC 전송 연결
    private PlayerController playerController; // 로컬 입력 제어와 이동 방향
    private PlayerStateMachine stateMachine; // 이동·생사 상태
    private _PlayerNetworkManager manager; // 등록·슬롯·카메라 관리자
    private _PlayerSnapShotPlayback playerSnapShotPlayback; // 보간값을 계산하는 일반 C# 객체
    private bool isInitialized; // 등록 완료 여부
    private bool isLocal; // 생성 당시 로컬 역할
    private bool isControlSuspended; // 퇴장/관리자 종료 시 제어 중단 여부
    private int actorNumber; // 생성자의 방 안 번호
    private int spawnSlot = PlayerNetworkConstants.c_unassignedSpawnSlot; // 생성 데이터의 시작 슬롯

    public int ActorNumber => actorNumber;
    public int SpawnSlot => spawnSlot;
    public GameObject PlayerCamera => playerCamera;
    public bool IsInitialized => isInitialized;
    public bool IsMine => photonView != null && photonView.IsMine;

    /// <summary>등록 완료·씬 관리자·현재 제어 권한이 모두 유효할 때만 입력을 허용합니다.</summary>
    public bool IsLocalControlActive => isInitialized && isActiveAndEnabled && isLocal
        && !isControlSuspended && manager != null && manager.isActiveAndEnabled
        && PhotonNetwork.InRoom && IsMine && photonView.ControllerActorNr == actorNumber;

    private void OnValidate()
    {
        if (float.IsNaN(bufferDelay) || float.IsInfinity(bufferDelay))
        {
            bufferDelay = 0.1f;
        }
        bufferDelay = Mathf.Max(0f, bufferDelay);
    }

    /// <summary>참조만 확보하고 PUN 생성·관리자 등록 전에는 입력과 카메라를 닫습니다.</summary>
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        playerLook = GetComponent<PlayerLook>();
        playerMotor = GetComponent<PlayerMotor>();
        playerController = GetComponent<PlayerController>();
        stateMachine = GetComponent<PlayerStateMachine>();
        body = transform;
        PlayerInput input = GetComponent<PlayerInput>();
        if (input != null)
        {
            input.enabled = false;
        }
        if (playerCamera != null)
        {
            playerCamera.SetActive(false);
        }
        if (photonView == null || playerController == null || playerLook == null
            || playerMotor == null || stateMachine == null || animationController == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[_PlayerNetworkSync] 이동·상태·표시 필수 참조가 없습니다.", this);
#endif
            enabled = false;
            return;
        }
        playerSnapShotPlayback = new _PlayerSnapShotPlayback(bufferDelay);
    }

    private void OnEnable()
    {
        if (actorNumber > 0)
        {
            Initialize(_PlayerNetworkManager.Instance);
        }
        playerController?.SetLocalControlActive(IsLocalControlActive);
    }

    private void OnDisable()
    {
        playerController?.SetLocalControlActive(false);
        if (isInitialized)
        {
            manager?.Unregister(this);
            isInitialized = false;
        }
        playerSnapShotPlayback?.Clear();
    }

    private void OnDestroy()
    {
        if (isInitialized)
        {
            manager?.Unregister(this);
            isInitialized = false;
        }
    }

    public void SuspendLocalControl()
    {
        isControlSuspended = true;
        playerController?.SetLocalControlActive(false);
    }

    /// <summary>생성 슬롯과 생성자 소유권을 확인한 뒤 등록을 시작합니다.</summary>
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        object[] data = photonView.InstantiationData;
        if (!isActiveAndEnabled || data == null || data.Length != 1 || !(data[0] is int slot)
            || slot < 0 || slot >= PlayerNetworkConstants.c_spawnSlotCount
            || info.Sender == null || info.Sender.ActorNumber != photonView.OwnerActorNr)
        {
#if UNITY_EDITOR
            Debug.LogError("[_PlayerNetworkSync] 생성 슬롯 또는 생성자의 소유권이 올바르지 않습니다.", this);
#endif
            return;
        }
        actorNumber = info.Sender.ActorNumber;
        spawnSlot = slot;
        Initialize(_PlayerNetworkManager.Instance);
    }

    /// <summary>등록이 성공해야 로컬 입력과 원격 스냅샷 재생을 시작합니다.</summary>
    private void Initialize(_PlayerNetworkManager targetManager)
    {
        if (isInitialized || !CanInitialize(targetManager))
        {
            return;
        }
        manager = targetManager;
        isLocal = IsMine;
        isControlSuspended = false;
        isInitialized = true;
        if (!manager.Register(this))
        {
            isInitialized = false;
            return;
        }
        playerController.SetLocalControlActive(IsLocalControlActive);
        if (isLocal && mineModel != null)
        {
            mineModel.SetActive(false);
        }
    }

    private bool CanInitialize(_PlayerNetworkManager targetManager)
    {
        bool ready = isActiveAndEnabled && targetManager != null && targetManager.isActiveAndEnabled
            && targetManager.gameObject.scene == gameObject.scene && playerCamera != null
            && playerController != null && playerController.IsInitialized && playerController.isActiveAndEnabled
            && stateMachine != null && stateMachine.isActiveAndEnabled && playerMotor != null && playerMotor.CanSimulate
            && playerLook != null && playerLook.isActiveAndEnabled
            && animationController != null && animationController.isActiveAndEnabled && playerSnapShotPlayback != null
            && PhotonNetwork.InRoom && photonView.ViewID != 0 && spawnSlot >= 0;
#if UNITY_EDITOR
        if (!ready)
        {
            Debug.LogError("[_PlayerNetworkSync] 생성 준비 또는 씬 관리자/카메라 참조가 없습니다.", this);
        }
#endif
        return ready;
    }

    /// <summary>받은 스냅샷의 보간된 위치·회전·이동 상태를 원격 플레이어에 적용합니다.</summary>
    private void Update()
    {
        if (!isInitialized || isLocal || !PhotonNetwork.InRoom)
        {
            return;
        }
        double now = PhotonNetwork.Time;
        _PlayerSnapShot? result = playerSnapShotPlayback.GetPlaybackSnapShot(now);
        if (result.HasValue)
        {
            ApplySnapShot(result.Value);
        }
    }

    /// <summary>7개의 기본 타입을 같은 순서로 송수신합니다.</summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (!IsLocalControlActive)
            {
                return;
            }
            _PlayerSnapShot snapshot = CaptureSnapshot(PhotonNetwork.Time);
            stream.SendNext(snapshot.Time);
            stream.SendNext(snapshot.Position);
            stream.SendNext(snapshot.YRotation);
            stream.SendNext(snapshot.ViewPitch);
            stream.SendNext((int)snapshot.State);
            stream.SendNext(snapshot.MoveDirection);
            stream.SendNext(snapshot.IsCrouching);
        }
        else
        {
            var snapshot = new _PlayerSnapShot((double)stream.ReceiveNext(),
                (Vector3)stream.ReceiveNext(), (float)stream.ReceiveNext(), (float)stream.ReceiveNext(),
                (PlayerState)(int)stream.ReceiveNext(), (Vector2)stream.ReceiveNext(),
                (bool)stream.ReceiveNext());

            playerSnapShotPlayback.PushSnapShot(snapshot);
        }
    }

    private _PlayerSnapShot CaptureSnapshot(double time)
    {
        return new _PlayerSnapShot(time, body.position, body.eulerAngles.y, playerLook.PitchDegrees,
            stateMachine.CurrentState, playerController.MoveDirection, playerMotor.IsCrouching);
    }

    /// <summary>계산된 위치·회전과 확정된 이동 상태를 Sync에서 적용합니다.</summary>
    private void ApplySnapShot(_PlayerSnapShot snapshot)
    {
        body.SetPositionAndRotation(snapshot.Position, Quaternion.Euler(0f, snapshot.YRotation, 0f));
        playerLook.SetPitch(snapshot.ViewPitch);
        playerMotor.SetCrouching(snapshot.IsCrouching);
        stateMachine.SetState(snapshot.State);
        animationController.SetMoveDirection(snapshot.MoveDirection);
    }

}
