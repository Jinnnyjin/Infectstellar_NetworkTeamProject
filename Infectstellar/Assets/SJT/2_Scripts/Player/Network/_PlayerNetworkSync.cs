using UnityEngine;
using Photon.Pun;
using ENZEUN.Runtime;

/// <summary>
/// 스냅샷 송수신을 받은 주체
/// 스냅샷을 수신 후 받은 기록을 Playback에 전달
/// </summary>
public class _PlayerNetworkSync : MonoBehaviour, IPunObservable, IPunInstantiateMagicCallback
{
    [SerializeField] private float bufferDelay = 0.1f;
    private _PlayerSnapShotPlayback playerSnapShotPlayback; // 전달받은 스냅샷을 전달하여 보간/외삽을 진행할 컴포넌트
    private bool isInitialized;
    private int actorNumber;
    private PlayerNetworkSyncManager registeredManager;

    private PhotonView photonView; // 이 플레이어의 조작 권한 확인과 RPC 전송에 쓰는 PUN 컴포넌트
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerMotor playerMotor;
    [SerializeField] private Transform body;
    private PlayerController playerController;
    private PlayerStateMachine stateMachine;

    [SerializeField, Tooltip("StateMachine에 연결한 것과 같은 모델의 애니메이션 컴포넌트")]
    private PlayerAnimationController animationController;
    [SerializeField] private GameObject mineModel;
    [SerializeField] private Renderer playerRenderer;

    [SerializeField] private GameObject pointOfView;
    [SerializeField] private GameObject thirdPersonView;

    public int ActorNumber => actorNumber;

    /// <summary>현재 클라이언트가 이 플레이어를 제어하는지 반환합니다.</summary>
    public bool IsMine => photonView != null && photonView.IsMine;

    /// <summary>네트워크 생성이 완료되고 내 제어 권한이 유효한지 반환합니다.</summary>
    public bool IsLocalControlActive => isInitialized && isActiveAndEnabled && PhotonNetwork.InRoom && IsMine && photonView.ControllerActorNr == actorNumber;
    /// <summary>이 플레이어의 현재 생존 여부를 반환합니다.</summary>
    public bool IsAlive => stateMachine != null && stateMachine.IsAlive;
    public GameObject MineModel => mineModel;
    public GameObject PointOfView => pointOfView;
    public GameObject ThirdPersonView => thirdPersonView;

    /// <summary>송수신과 원격 자세 적용에 필요한 참조를 확보합니다.</summary>
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        playerLook = GetComponent<PlayerLook>();
        playerMotor = GetComponent<PlayerMotor>();
        playerController = GetComponent<PlayerController>();
        stateMachine = GetComponent<PlayerStateMachine>();
        body = transform;

        if (photonView == null || playerLook == null || playerMotor == null
            || playerController == null || stateMachine == null
            || animationController == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[_PlayerNetworkSync] 이동·상태·애니메이션의 필수 참조가 없습니다.", this);
#endif
            enabled = false;
            return;
        }

        // 카메라 관리자가 선택한 카메라만 켜도록 초기에는 모두 끕니다.
        if (pointOfView != null)
        {
            pointOfView.SetActive(false);
        }
        if (thirdPersonView != null && thirdPersonView != pointOfView)
        {
            thirdPersonView.SetActive(false);
        }
#if UNITY_EDITOR
        if (pointOfView == null || thirdPersonView == null || pointOfView == thirdPersonView)
        {
            Debug.LogWarning("[_PlayerNetworkSync] 본인 시점과 삼인칭 시점에 서로 다른 카메라를 연결해야 합니다. 삼인칭 카메라가 없으면 관전 대상에서 제외합니다.", this);
        }
#endif

        playerSnapShotPlayback = new _PlayerSnapShotPlayback(bufferDelay);
    }

    /// <summary>재활성화 시 현재 권한에 맞춰 로컬 입력을 설정합니다.</summary>
    private void OnEnable()
    {
        playerController?.SetLocalControlActive(IsLocalControlActive);
        if (isInitialized)
        {
            RegisterPlayer();
        }
    }

    /// <summary>비활성화 시 로컬 입력과 이전 재생 기록을 정리합니다.</summary>
    private void OnDisable()
    {
        playerController?.SetLocalControlActive(false);
        playerSnapShotPlayback?.Clear();
        if (registeredManager != null)
        {
            registeredManager.UnRegister(this);
            registeredManager = null;
        }
    }

    /// <summary>원격 플레이어의 보간 또는 외삽된 자세를 매 프레임 적용합니다.</summary>
    private void Update()
    {
        if (!PhotonNetwork.InRoom || IsMine || playerSnapShotPlayback == null)
        {
            return;
        }

        _PlayerSnapShot? result = playerSnapShotPlayback.GetPlaybackSnapShot(PhotonNetwork.Time);
        if (result.HasValue)
        {
            ApplySnapShot(result.Value);
        }
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        if (!isActiveAndEnabled || playerController == null || !playerController.IsInitialized)
        {
            return;
        }

        actorNumber = info.Sender.ActorNumber;
        playerSnapShotPlayback?.Clear();

        isInitialized = true;
        playerController.SetLocalControlActive(IsLocalControlActive);

        int colorIndex = (int)info.Sender.CustomProperties[PhotonLinkManager.myColor_Customproperty];
        Color newColor = PhotonLinkManager.GetIndexedColor(colorIndex);

        var block = new MaterialPropertyBlock();
        playerRenderer.GetPropertyBlock(block, 1);
        block.SetColor("_BaseColor", newColor);
        playerRenderer.SetPropertyBlock(block, 1);

        // 기존 모델 처리 유지
        if (IsMine)
        {
            if (mineModel != null)
                mineModel.SetActive(false);
        }

        RegisterPlayer();
    }

    /// <summary>네트워크 초기화가 끝난 플레이어를 씬 관리자에 등록합니다.</summary>
    private void RegisterPlayer()
    {
        if (!isInitialized || !PhotonNetwork.InRoom)
        {
            return;
        }

        registeredManager = PlayerNetworkSyncManager.Instance;
        if (registeredManager == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[_PlayerNetworkSync] PlayerNetworkSyncManager가 없습니다.", this);
#endif
            return;
        }

        registeredManager.Register(this);
    }

    /// <summary>자세·이동 상태와 외삽에 사용할 실제 속도를 송수신합니다.</summary>
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!isActiveAndEnabled || playerSnapShotPlayback == null)
        {
            return;
        }

        if (stream.IsWriting)
        {
            _PlayerSnapShot sendSnapShot = new _PlayerSnapShot(
                PhotonNetwork.Time,
                body.position, body.eulerAngles.y, playerLook.PitchDegrees,
                stateMachine.CurrentState, playerController.MoveDirection, playerMotor.IsCrouching,
                playerMotor.Velocity);

            stream.SendNext(sendSnapShot.Time);

            stream.SendNext(sendSnapShot.Position);
            stream.SendNext(sendSnapShot.YRotation);
            stream.SendNext(sendSnapShot.ViewPitch);

            stream.SendNext((int)sendSnapShot.State);
            stream.SendNext(sendSnapShot.MoveDirection);
            stream.SendNext(sendSnapShot.IsCrouching);

            stream.SendNext(sendSnapShot.Velocity);
        }
        else
        {
            _PlayerSnapShot receiveSnapShot = new _PlayerSnapShot(
                (double)stream.ReceiveNext(),
                (Vector3)stream.ReceiveNext(), (float)stream.ReceiveNext(), (float)stream.ReceiveNext(),
                (PlayerState)(int)stream.ReceiveNext(), (Vector2)stream.ReceiveNext(), (bool)stream.ReceiveNext(),
                (Vector3)stream.ReceiveNext());

            playerSnapShotPlayback.PushSnapShot(receiveSnapShot);
        }
    }

    /// <summary>재생 시각에 맞춘 위치·회전·이동 상태를 원격 플레이어에 적용합니다.</summary>
    private void ApplySnapShot(_PlayerSnapShot snapShot)
    {
        body.SetPositionAndRotation(
            snapShot.Position,
            Quaternion.Euler(0f, snapShot.YRotation, 0f));

        playerLook.SetPitch(snapShot.ViewPitch);

        playerMotor.SetCrouching(snapShot.IsCrouching);
        stateMachine.SetState(snapShot.State);
        animationController.SetMoveDirection(snapShot.MoveDirection);
    }
}
