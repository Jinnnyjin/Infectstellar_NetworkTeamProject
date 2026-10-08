using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 자신의 입력 인스턴스를 관리하고 HFSM과 이동의 프레임 처리를 시작합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateMachine), typeof(PlayerMotor))]
public sealed class PlayerController : MonoBehaviour
{
    private const string c_moveAction = "Player/Move";
    private const string c_sprintAction = "Player/Sprint";
    private const string c_crouchAction = "Player/Crouch";
    private const string c_jumpAction = "Player/Jump";

    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private PlayerMovementSettings movementSettings;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerMotor motor;

    private InputActionAsset runtimeActions;
    private InputActionMap playerActions;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    private bool isInitialized;
    private bool wasJumpHeld;

    /// <summary>
    /// 필수 참조와 설정을 확인하고 입력 자산을 플레이어 전용으로 복제합니다.
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

        if (inputActions == null || movementSettings == null || stateMachine == null || motor == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerController] Input Actions, 이동 설정, StateMachine 또는 Motor 참조가 없습니다.", this);
#endif
            enabled = false;
            return;
        }

        if (stateMachine.gameObject != gameObject || motor.gameObject != gameObject)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerController] StateMachine과 Motor는 같은 플레이어 객체에 있어야 합니다.", this);
#endif
            enabled = false;
            return;
        }

        if (!motor.Initialize(movementSettings))
        {
            enabled = false;
            return;
        }

        runtimeActions = Instantiate(inputActions);
        moveAction = runtimeActions.FindAction(c_moveAction);
        sprintAction = runtimeActions.FindAction(c_sprintAction);
        crouchAction = runtimeActions.FindAction(c_crouchAction);
        jumpAction = runtimeActions.FindAction(c_jumpAction);
        if (moveAction == null || sprintAction == null || crouchAction == null || jumpAction == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerController] Player 맵의 Move, Sprint, Crouch, Jump 액션이 필요합니다.", this);
#endif
            Destroy(runtimeActions);
            runtimeActions = null;
            enabled = false;
            return;
        }

        sprintAction.wantsInitialStateCheck = true;
        crouchAction.wantsInitialStateCheck = true;
        playerActions = moveAction.actionMap;
        stateMachine.Initialize(motor, movementSettings);
        isInitialized = true;
    }

    /// <summary>
    /// 활성 기간에만 입력을 받고 재활성화 시 홀드 입력을 새 점프로 취급하지 않습니다.
    /// </summary>
    private void OnEnable()
    {
        if (!isInitialized)
        {
            enabled = false;
            return;
        }

        playerActions.Enable();
        wasJumpHeld = false;
        foreach (InputControl control in jumpAction.controls)
        {
            if (control is ButtonControl button && button.isPressed)
            {
                wasJumpHeld = true;
                break;
            }
        }
    }

    /// <summary>
    /// 입력을 한 번 읽고 상태 결정과 물리 처리를 한 번 진행합니다.
    /// </summary>
    private void Update()
    {
        if (stateMachine == null || motor == null || movementSettings == null || !stateMachine.isActiveAndEnabled || !motor.CanSimulate)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerController] 필수 컴포넌트 또는 이동 설정을 사용할 수 없어 입력 처리를 중지합니다.", this);
#endif
            enabled = false;
            return;
        }

        bool jumpHeld = jumpAction.IsPressed();
        bool jumpPressed = jumpAction.WasPressedThisFrame() && !wasJumpHeld;
        wasJumpHeld = jumpHeld;
        var input = new PlayerInputFrame(moveAction.ReadValue<Vector2>(), sprintAction.IsPressed(), crouchAction.IsPressed(), jumpPressed);
        stateMachine.Tick(input, Time.deltaTime);
    }

    /// <summary>
    /// 비활성화 동안 입력 상태와 액션을 정리합니다.
    /// </summary>
    private void OnDisable()
    {
        playerActions?.Disable();
        wasJumpHeld = false;
    }

    /// <summary>
    /// 공유 원본을 유지하고 자신이 복제한 입력 인스턴스만 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (runtimeActions != null)
        {
            runtimeActions.Disable();
            Destroy(runtimeActions);
            runtimeActions = null;
        }
    }
}
