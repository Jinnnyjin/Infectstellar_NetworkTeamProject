using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInput))]
public sealed class PlayerInputReader : MonoBehaviour
{
    #region 입력 설정 및 참조
    // 플레이어 입력에 사용할 인풋시스템의 액션 맵
    private const string c_PlayerMap = "Player";
    // Control Scheme은 어떤 입력 장치 조합을 사용하는지 구분하는 설정. Keyboard&Mouse에 설정한 장치들의 방식만 허용함.
    private const string c_keyboardMouseScheme = "Keyboard&Mouse";
    private const string c_moveActionName = "Move";
    private const string c_lookActionName = "Look";
    private const string c_sprintActionName = "Sprint";
    private const string c_crouchActionName = "Crouch";
    private const string c_jumpActionName = "Jump";

    private PlayerInput playerInput;
    private InputActionAsset cachedActions; // 어느 액션 자산에서 참조를 찾아 두었는지
    private InputActionMap playerMap; // "Player"로 찾은 액션 맵 자체의 참조
    #endregion
    #region InputActions, 각 조작을 읽을 대상의 액션을 참조함
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    #endregion
    #region 읽은 입력 결과와 조작 허용 상태를 저장하는 필드
    private PlayerInputFrame currentFrame; // 마지막으로 읽은 이동·달리기·웅크리기·점프 입력
    private Vector2 lookDelta; // 시점 회전에 사용할 이번 프레임의 마우스 이동량
    private bool isControlActive; // Controller가 플레이어 조작을 허용했는지
    private bool shouldBlockJumpUntilRelease = true; // 이미 누른 점프는 버튼을 놓을 때까지 차단할지, 예를 들어 Space를 누른 상태로 게임 창에 돌아와 조작을 재개한 경우, 이때 원본은 이미 누르고 있던 Space를 새로운 점프 요청으로 받아들이지 않도록 함
    #endregion

    public PlayerInputFrame CurrentFrame => currentFrame;
    public Vector2 LookDelta => lookDelta;

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
    /// 게임 창의 포커스가 바뀌면 Unity가 자동으로 호출하는 메서드
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
    /// Controller의 활성 기간과 커서 잠금을 함께 시작하거나 종료
    /// </summary>
    public void SetControlActive(bool isActive)
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

        // InputActions가 제대로 설정되지 않았다면 컴포넌트를 비활성화하고 종료
        if (!CacheActions())
        {
            enabled = false;
            return;
        }

        // 1. 현재 Control Scheme이 허용한 Keyboard&Mouse 방식과 다른지
        // 2. 현재 Control Scheme에서 필수로 요구하는 입력 장치가 부족한지
        // 3. PlayerInput의 현재 액션 맵이 캐싱한 Player 맵과 다른지
        // 4. Player 맵에 활성화된 액션이 하나도 없는지
        if (playerInput.currentControlScheme != c_keyboardMouseScheme || playerInput.hasMissingRequiredDevices
           || playerInput.currentActionMap != playerMap || !playerMap.enabled)
        {
            shouldBlockJumpUntilRelease = true;
            SetCursorLocked(false);
            return;
        }

        if (HandleCursorControl())
        {
            return;
        }

        bool jumpHeld = jumpAction.enabled && IsButtonHeld(jumpAction);
        bool jumpPressed = jumpAction.enabled && jumpAction.WasPressedThisFrame() && !shouldBlockJumpUntilRelease;
        shouldBlockJumpUntilRelease = jumpHeld;
        currentFrame = new PlayerInputFrame(
            moveAction.enabled ? moveAction.ReadValue<Vector2>() : Vector2.zero,
            sprintAction.enabled && IsButtonHeld(sprintAction),
            crouchAction.enabled && IsButtonHeld(crouchAction),
            jumpPressed);
        lookDelta = lookAction.enabled ? lookAction.ReadValue<Vector2>() : Vector2.zero;
    }

    /// <summary>
    /// 바인딩된 버튼의 홀드를 읽어 액션 재활성화 때도 달리기·웅크림을 유지합니다.
    /// </summary>
    /// <param name="action">홀딩 중인지 여부를 판단하려는 InputAction</param>
    /// <returns>해당 InputAction이 홀딩되어 있는지</returns>
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
    /// 커서 해제·재잠금을 처리하고, 일반 입력을 중단해야 하면 true를 반환합니다.
    /// </summary>
    /// <returns>커서가 풀려 있거나 클릭으로 방금 다시 잠겨 이번 프레임의 조작을 건너뛰면 true, 커서가 이미 잠겨 있어 이동·시점 조작을 계속할 수 있으면 false</returns>
    private bool HandleCursorControl()
    {
        if (Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            shouldBlockJumpUntilRelease = true;
            SetCursorLocked(false);
            return true;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            shouldBlockJumpUntilRelease = true;

            if (Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame)
            {
                shouldBlockJumpUntilRelease = IsButtonHeld(jumpAction);
                SetCursorLocked(true);
            }

            // 재잠금한 프레임도 일반 입력을 전달하지 않습니다.
            return true;
        }

        return false;
    }

    /// <summary>
    /// 조작을 시작하거나 재활성화하는 순간, 점프 버튼을 이미 누르고 있는지 확인해서 차단 여부를 정하는 메서드
    /// ex : Space를 누른 상태로 게임 창에 돌아와 조작을 재개한 경우 등 대비
    /// </summary>
    private void CaptureJumpHold()
    {
        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput == null || !CacheActions())
        {
            shouldBlockJumpUntilRelease = true;
            return;
        }

        shouldBlockJumpUntilRelease = IsButtonHeld(jumpAction);
    }

    /// <summary>
    /// PlayerInput이 실제로 사용하는 인스턴스에서 필요한 액션을 한 번 찾음
    /// 필요한 액션들을 찾아서 사용할 준비를 하는 작업
    /// </summary>
    /// <returns>필요한 액션 참조를 확보하고, 준비 성공 여부</returns>
    private bool CacheActions()
    {
        InputActionAsset actions = playerInput.actions;
        // 이미 참조를 준비한 자산과 같은가 확인
        if (actions != null && cachedActions == actions)
        {
            return true;
        }

        playerMap = actions != null ? actions.FindActionMap(c_PlayerMap) : null;
        moveAction = playerMap?.FindAction(c_moveActionName);
        lookAction = playerMap?.FindAction(c_lookActionName);
        sprintAction = playerMap?.FindAction(c_sprintActionName);
        crouchAction = playerMap?.FindAction(c_crouchActionName);
        jumpAction = playerMap?.FindAction(c_jumpActionName);

        if (moveAction == null || lookAction == null || sprintAction == null || crouchAction == null || jumpAction == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerInputReader] PlayerInput에 Player 맵의 Move, Look, Sprint, Crouch, Jump 액션을 연결해야 합니다.", this);
#endif
            return false;
        }

        cachedActions = actions;
        return true;
    }

    /// <summary>
    /// 지난 입력을 비우고 조작 재개 때는 점프 버튼을 놓을 때까지 기다림.
    /// </summary>
    private void ResetInput()
    {
        currentFrame = default;
        lookDelta = Vector2.zero;
        shouldBlockJumpUntilRelease = true;
    }

    /// <summary>
    /// 커서 잠금과 표시를 같은 정책으로 설정
    /// </summary>
    /// <param name="isLocked">커서를 잠글지 여부</param>
    private void SetCursorLocked(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}
