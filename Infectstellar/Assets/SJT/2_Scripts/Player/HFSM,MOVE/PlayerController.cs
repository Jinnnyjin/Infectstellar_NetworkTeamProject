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

    private PlayerInputReader inputReader;
    private PlayerLook playerLook;
    private FlashlightController flashlightController;
    private bool isInitialized;

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

        inputReader = GetComponent<PlayerInputReader>();
        playerLook = GetComponent<PlayerLook>();
        flashlightController = GetComponent<FlashlightController>();
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

        inputReader.SetControlActive(true);
        if (animationController != null)
        {
            animationController.SetMoveDirection(Vector2.zero);
        }
    }

    /// <summary>
    /// 회전을 먼저 반영하여 이번 프레임의 이동이 새 몸 방향을 따르게 합니다.
    /// </summary>
    private void Update()
    {
        if (stateMachine == null || motor == null || movementSettings == null || inputReader == null || playerLook == null
            || !stateMachine.isActiveAndEnabled || !motor.CanSimulate)
        {
            Debug.LogError("[PlayerController] 필수 컴포넌트 또는 이동 설정을 사용할 수 없어 처리를 중지합니다.", this);
            enabled = false;
            return;
        }

        inputReader.ReadInput();
        if (stateMachine.IsAlive)
        {
            playerLook.Rotate(inputReader.LookDelta);
            if (inputReader.FlashPressed && flashlightController != null && flashlightController.isActiveAndEnabled)
            {
                flashlightController.ToggleFlashlight();
            }
        }

        stateMachine.Tick(inputReader.CurrentFrame, Time.deltaTime);
        if (animationController != null)
        {
            animationController.SetMoveDirection(inputReader.CurrentFrame.Move);
        }

#if UNITY_EDITOR
        // 잡기 테스트용 코드
        if (Input.GetKeyDown(KeyCode.O))
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
        if (animationController != null)
        {
            animationController.SetMoveDirection(Vector2.zero);
        }

        if (inputReader != null)
        {
            inputReader.SetControlActive(false);
        }
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
        if (currentState == PlayerState.Dead && flashlightController != null)
        {
            flashlightController.SetFlashlightEnabled(false);
        }
    }
}
