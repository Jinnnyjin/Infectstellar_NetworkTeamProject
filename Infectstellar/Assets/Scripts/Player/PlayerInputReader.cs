using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// PlayerInput의 입력을 한 프레임 값으로 보관하고 커서 잠금에 따라 조작을 제한합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class PlayerInputReader : MonoBehaviour
{
    private const string c_playerMap = "Player";
    private const string c_keyboardMouseScheme = "Keyboard&Mouse";

    private PlayerInput playerInput;
    private InputActionAsset cachedActions;
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    private PlayerInputFrame currentFrame;
    private Vector2 lookDelta;
    private bool isControlActive;
    private bool shouldBlockJumpUntilRelease = true;

    /// <summary>
    /// 마지막 ReadInput 호출에서 적재한 이동·버튼 입력입니다.
    /// </summary>
    public PlayerInputFrame CurrentFrame => currentFrame;

    /// <summary>
    /// 이번 프레임의 마우스 이동량입니다. 단위는 픽셀입니다.
    /// </summary>
    public Vector2 LookDelta => lookDelta;

    /// <summary>
    /// 액션의 활성화와 장치 연결을 담당하는 Unity 컴포넌트를 찾습니다.
    /// </summary>
    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    /// <summary>
    /// Controller가 조작을 요청한 상태라면 재활성화 시 커서를 잠급니다.
    /// </summary>
    private void OnEnable()
    {
        if (isControlActive && Application.isFocused)
        {
            CaptureJumpHold();
            SetCursorLocked(true);
        }
    }

    /// <summary>
    /// 비활성 플레이어가 이전 입력이나 커서 잠금을 유지하지 않게 합니다.
    /// </summary>
    private void OnDisable()
    {
        ResetInput();
        SetCursorLocked(false);
    }

    /// <summary>
    /// 포커스를 잃으면 조작을 해제합니다. 복귀 후 클릭으로 다시 시작합니다.
    /// </summary>
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ResetInput();
            SetCursorLocked(false);
        }
    }

    /// <summary>
    /// Controller의 활성 기간과 커서 잠금을 함께 시작하거나 종료합니다.
    /// </summary>
    internal void SetControlActive(bool isActive)
    {
        isControlActive = isActive;
        ResetInput();
        if (isActive)
        {
            CaptureJumpHold();
        }

        SetCursorLocked(isActive && isActiveAndEnabled && Application.isFocused);
    }

    /// <summary>
    /// Controller가 Update 시작에 한 번 호출하여 이번 프레임의 입력을 적재합니다.
    /// </summary>
    public void ReadInput()
    {
        currentFrame = default;
        lookDelta = Vector2.zero;
        if (!isControlActive || !isActiveAndEnabled || !Application.isFocused
            || playerInput == null || !playerInput.isActiveAndEnabled || !playerInput.inputIsActive)
        {
            shouldBlockJumpUntilRelease = true;
            SetCursorLocked(false);
            return;
        }

        if (!CacheActions())
        {
            enabled = false;
            return;
        }

        if (playerInput.currentControlScheme != c_keyboardMouseScheme || playerInput.hasMissingRequiredDevices
            || playerInput.currentActionMap != playerMap || !playerMap.enabled)
        {
            shouldBlockJumpUntilRelease = true;
            SetCursorLocked(false);
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            shouldBlockJumpUntilRelease = true;
            SetCursorLocked(false);
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            shouldBlockJumpUntilRelease = true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                shouldBlockJumpUntilRelease = IsButtonHeld(jumpAction);
                SetCursorLocked(true);
            }

            // 잠금에 사용한 클릭 프레임의 마우스 delta도 버립니다.
            return;
        }

        bool jumpHeld = jumpAction.enabled && IsButtonHeld(jumpAction);
        bool jumpPressed = jumpAction.enabled && jumpAction.WasPressedThisFrame() && !shouldBlockJumpUntilRelease;
        shouldBlockJumpUntilRelease = jumpHeld;
        currentFrame = new PlayerInputFrame(
            moveAction.enabled ? moveAction.ReadValue<Vector2>() : Vector2.zero,
            sprintAction.enabled && IsButtonHeld(sprintAction), crouchAction.enabled && IsButtonHeld(crouchAction), jumpPressed);
        lookDelta = lookAction.enabled ? lookAction.ReadValue<Vector2>() : Vector2.zero;
    }

    /// <summary>
    /// PlayerInput이 실제로 사용하는 인스턴스에서 필요한 액션을 한 번 찾습니다.
    /// </summary>
    private bool CacheActions()
    {
        InputActionAsset actions = playerInput.actions;
        if (actions != null && cachedActions == actions)
        {
            return true;
        }

        playerMap = actions != null ? actions.FindActionMap(c_playerMap) : null;
        moveAction = playerMap?.FindAction("Move");
        lookAction = playerMap?.FindAction("Look");
        sprintAction = playerMap?.FindAction("Sprint");
        crouchAction = playerMap?.FindAction("Crouch");
        jumpAction = playerMap?.FindAction("Jump");
        if (moveAction == null || lookAction == null || sprintAction == null || crouchAction == null || jumpAction == null)
        {
            Debug.LogError("[PlayerInputReader] PlayerInput에 Player 맵의 Move, Look, Sprint, Crouch, Jump 액션을 연결해야 합니다.", this);
            return false;
        }

        cachedActions = actions;
        return true;
    }

    /// <summary>
    /// 바인딩된 버튼의 홀드를 읽어 액션 재활성화 때도 달리기·웅크림을 유지합니다.
    /// </summary>
    private bool IsButtonHeld(InputAction action)
    {
        if (action == null)
        {
            return false;
        }

        foreach (InputControl control in action.controls)
        {
            if (control is ButtonControl button && button.isPressed)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 재개 순간 이미 눌린 점프만 차단하며 액션 캐시는 첫 ReadInput에서 확보합니다.
    /// </summary>
    private void CaptureJumpHold()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        shouldBlockJumpUntilRelease = IsButtonHeld(playerInput.actions?.FindAction("Player/Jump"));
    }

    /// <summary>
    /// 지난 입력을 비우고 조작 재개 때는 점프 버튼을 놓을 때까지 기다립니다.
    /// </summary>
    private void ResetInput()
    {
        currentFrame = default;
        lookDelta = Vector2.zero;
        shouldBlockJumpUntilRelease = true;
    }

    /// <summary>
    /// 커서 잠금과 표시를 같은 정책으로 설정합니다.
    /// </summary>
    private void SetCursorLocked(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}
