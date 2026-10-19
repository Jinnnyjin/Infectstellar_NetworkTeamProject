using UnityEngine;

/// <summary>
/// 입력 적재 → 몸과 시점 회전 → 기존 상태머신의 이동 처리를 순서대로 시작합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateMachine), typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerInputReader), typeof(PlayerLook))]
public sealed class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerMovementSettings movementSettings;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerMotor motor;
    [SerializeField, Tooltip("StateMachine과 같은 모델의 애니메이션 컨트롤러를 연결")]
    private PlayerAnimationController animationController;
    private Health health;
    private PlayerInputReader inputReader;
    private PlayerLook playerLook;
    private FlashlightController flashlightController;
    private PlayerGrabber playerGrabber;
    private bool isInitialized;
    private bool isLocalControlActive;
    private _PlayerNetworkSync networkSync;

    /// <summary>필수 이동 컴포넌트의 초기화 성공 여부입니다.</summary>
    public bool IsInitialized => isInitialized;

    /// <summary>스냅샷에 사용할 마지막 이동 방향입니다.</summary>
    public Vector2 MoveDirection => inputReader != null ? inputReader.CurrentFrame.Move : Vector2.zero;

    /// <summary>
    /// 필요한 컴포넌트를 찾고 기존 이동 설정과 상태머신을 초기화합니다.
    /// </summary>
    private void Awake()
    {
        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        if (motor == null)
        {
            motor = GetComponent<PlayerMotor>();
        }

        health = GetComponent<Health>();
        inputReader = GetComponent<PlayerInputReader>();
        playerLook = GetComponent<PlayerLook>();
        flashlightController = GetComponent<FlashlightController>();
        playerGrabber = GetComponent<PlayerGrabber>();
        networkSync = GetComponent<_PlayerNetworkSync>();
        if (movementSettings == null || stateMachine == null || motor == null || inputReader == null || playerLook == null)
        {
            Debug.LogError("[PlayerController] 이동 설정, StateMachine, Motor, InputReader 또는 Look 참조가 없습니다.", this);
            enabled = false;
            return;
        }

        if (stateMachine.gameObject != gameObject || motor.gameObject != gameObject)
        {
            Debug.LogError("[PlayerController] StateMachine과 Motor는 같은 플레이어 객체에 있어야 합니다.", this);
            enabled = false;
            return;
        }

        if (!motor.Initialize(movementSettings))
        {
            enabled = false;
            return;
        }

        stateMachine.Initialize(motor, movementSettings);
        stateMachine.StateChanged += HandlePlayerStateChanged;
        isInitialized = true;
    }

    /// <summary>
    /// 준비된 플레이어의 입력과 커서 잠금을 시작합니다.
    /// </summary>
    private void OnEnable()
    {
        // Awake에서 초기화에 실패한 경우, 컴포넌트의 활성화를 해제한다.
        if (!isInitialized)
        {
            enabled = false;
            return;
        }

        // 추가
        if (health != null)
        {
            health.D_Died += HandleDied;
            health.D_Revived += HandleRevived;
        }

        SetLocalControlActive(networkSync == null || networkSync.IsLocalControlActive);
        if (animationController != null)
        {
            animationController.SetMoveDirection(Vector2.zero);
        }

        if (playerGrabber != null)
        {
            playerGrabber.GrabRequested += animationController.SetHandsUp;
            playerGrabber.DropRequested += animationController.SetHandsUp;
            playerGrabber.ShootRequested += animationController.SetHandsUp;
        }
    }

    /// <summary>
    /// 회전을 먼저 반영하여 이번 프레임의 이동이 새 몸 방향을 따르게 합니다.
    /// </summary>
    private void Update()
    {
        if (!isLocalControlActive || (networkSync != null && !networkSync.IsLocalControlActive))
        {
            return;
        }

        if (stateMachine == null || motor == null || movementSettings == null || inputReader == null || playerLook == null
            || !stateMachine.isActiveAndEnabled || !motor.CanSimulate)
        {
            Debug.LogError("[PlayerController] 필수 컴포넌트 또는 이동 설정을 사용할 수 없어 처리를 중지합니다.", this);
            enabled = false;
            return;
        }

        inputReader.ReadInput();
        if (stateMachine.IsAlive) // 살았을 때 입력
        {
            playerLook.Rotate(inputReader.LookDelta);
            if (inputReader.FlashPressed && flashlightController != null && flashlightController.isActiveAndEnabled)
            {
                flashlightController.ToggleFlashlight();
            }
        }
        else // 죽었을 때 입력
        {
            if (inputReader.NextSpectateTargetPressed)
            {
                PlayerNetworkSyncManager.Instance?.ChangeToNextFocusedPlayer();
            }
        }

        stateMachine.Tick(inputReader.CurrentFrame, Time.deltaTime);
        if (animationController != null)
        {
            animationController.SetMoveDirection(inputReader.CurrentFrame.Move);
        }

#if UNITY_EDITOR
        // 잡기 테스트용 코드
        if (animationController != null && Input.GetKeyDown(KeyCode.O))
        {
            animationController.SetHandsUp(!animationController.IsHandsUp);
        }
#endif
    }

    /// <summary>
    /// Controller가 멈추면 입력 저장값과 커서 잠금도 해제합니다.
    /// </summary>
    private void OnDisable()
    {
        if (health != null)
        {
            health.D_Died -= HandleDied;
            health.D_Revived -= HandleRevived;
        }

        if (!isLocalControlActive)
        {
            return;
        }

        if (animationController != null)
        {
            animationController.SetMoveDirection(Vector2.zero);
        }

        if (inputReader != null)
        {
            inputReader.SetControlActive(false);
        }
        isLocalControlActive = false;
    }

    /// <summary>로컬 플레이어의 체력이 0이 되면 HFSM을 사망 상태로 전환합니다.</summary>
    private void HandleDied(DamageInfo damageInfo)
    {
        if (!isInitialized || stateMachine == null)
        {
            return;
        }

        // 원격 플레이어의 상태는 기존 스냅샷으로 재생합니다.
        if (networkSync != null && !networkSync.IsLocalControlActive)
        {
            return;
        }

        stateMachine.Die();
    }

    /// <summary>로컬 플레이어의 부활 알림을 상태머신에 전달합니다.</summary>
    private void HandleRevived()
    {
        if (!isInitialized || stateMachine == null)
        {
            return;
        }

        // 원격 플레이어의 상태는 기존 스냅샷으로 재생합니다.
        if (networkSync != null && !networkSync.IsLocalControlActive)
        {
            return;
        }

        stateMachine.Revive();
    }

#if UNITY_EDITOR
    [ContextMenu("Player Revive")]
#endif
    /// <summary>직접 제어하는 플레이어의 부활을 시도합니다.</summary>
    public bool TryRevive()
    {
        if (!isInitialized || !isActiveAndEnabled || health == null || !stateMachine.IsAlive)
        {
            return false;
        }

        if (networkSync != null && !networkSync.IsLocalControlActive)
        {
            return false;
        }

        return health.Revive();
    }

    /// <summary>초기화 후 로컬 입력만 시작하거나 중단합니다.</summary>
    public void SetLocalControlActive(bool isActive)
    {
        isActive = isActive && isInitialized && isActiveAndEnabled;
        if (isLocalControlActive == isActive)
        {
            return;
        }

        isLocalControlActive = isActive;
        inputReader.SetControlActive(isActive);
    }

    /// <summary>
    /// 비활성 중에도 받던 사망 알림의 구독을 객체 파괴 시 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (stateMachine != null)
        {
            stateMachine.StateChanged -= HandlePlayerStateChanged;
        }
    }

    /// <summary>
    /// 사망 상태가 확정되면 즉시 손전등을 끕니다. 부활 때 다시 켜지 않습니다.
    /// </summary>
    private void HandlePlayerStateChanged(PlayerState previousState, PlayerState currentState)
    {
        bool isLocalPlayer = networkSync != null && networkSync.IsLocalControlActive;

        if (currentState == PlayerState.Dead)
        {
            if (flashlightController != null)
            {
                flashlightController.SetFlashlightEnabled(false);
            }
            if (playerGrabber != null)
            {
                playerGrabber.TryDrop();
            }

            if (isLocalPlayer)
            {
                PlayerNetworkSyncManager.Instance?.SetFocusedPlayer(networkSync);
                CinemachineCameraController.Instance?.ShowNextThirdPersonView(networkSync);
            }
        }

        else if (previousState == PlayerState.Dead && isLocalPlayer)
        {
            // 내 플레이어가 부활했을 때 내 시점으로 복귀
            PlayerNetworkSyncManager.Instance?.SetFocusedPlayer(networkSync);
            CinemachineCameraController.Instance?.ShowLocalPointOfView();
        }
    }
}
