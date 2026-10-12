using UnityEngine;

/// <summary>
/// 외부에서 전달받은 상태와 이동 방향을 Animator에 적용합니다. 재생과 혼합은 Animator가 처리합니다.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimationController : MonoBehaviour
{
    #region Animator 해시 캐싱
    private static readonly int moveXHash = Animator.StringToHash("MoveX");
    private static readonly int moveYHash = Animator.StringToHash("MoveY");
    private static readonly int idleHash = Animator.StringToHash("Base Layer.Idle");
    private static readonly int walkHash = Animator.StringToHash("Base Layer.Walk");
    private static readonly int runHash = Animator.StringToHash("Base Layer.Run");
    private static readonly int crouchIdleHash = Animator.StringToHash("Base Layer.CrouchIdle");
    private static readonly int crouchWalkHash = Animator.StringToHash("Base Layer.CrouchWalk");
    private static readonly int jumpHash = Animator.StringToHash("Base Layer.Jump");
    private static readonly int fallHash = Animator.StringToHash("Base Layer.Fall");
    private static readonly int deadHash = Animator.StringToHash("Base Layer.Dead");
    #endregion

    [SerializeField, Tooltip("상태 전환과 방향 입력 완화에 사용하는 시간 설정")]
    private PlayerAnimationSettings settings;
    [SerializeField, Tooltip("이 컴포넌트와 같은 모델 객체의 Animator를 연결")]
    private Animator animator;

    private PlayerState requestedState; // HFSM이 마지막으로 요청한 상태로, 다음 Update에서 재생에 반영
    private PlayerState playedState; // Animator에 마지막으로 재생을 요청한 상태로, 같은 상태의 재시작을 막는 데 사용
    private Vector2 moveDirection; // 방향 애니메이션을 섞는 데 사용할 이동 입력. X는 좌우, Y는 앞뒤
    private bool hasPlayedState; // 이번 활성화에서 재생을 시작했는지 나타내며, 첫 재생은 혼합 없이 시작
    private bool shouldChangeState; // 다음 Update에서 처리할 상태 변경 요청이 대기 중인지 나타냄

    /// <summary>
    /// Animator의 연결을 확인하고 마지막으로 전달받은 상태부터 재생하도록 예약
    /// </summary>
    private void OnEnable()
    {
        if (settings == null || animator == null
            || animator.runtimeAnimatorController == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman
            || animator.gameObject != gameObject)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerAnimationController] : Inspector에 Settings와 같은 모델 객체의 Animator를 연결해야 합니다. Animator에는 유효한 Humanoid Avatar와 Controller가 필요합니다.");
#endif
            enabled = false;
            return;
        }

        if (animator.applyRootMotion)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerAnimationController] : 이동은 PlayerMotor가 처리하므로 Animator의 Apply Root Motion을 꺼야 합니다.");
#endif
            enabled = false;
            return;
        }

        shouldChangeState = true;
        hasPlayedState = false;
    }

    /// <summary>
    /// PlayerController의 입력/HFSM 갱신 뒤, Animator 평가 전에 방향과 예약된 상태를 전달합니다.
    /// </summary>
    private void Update()
    {
        if (animator == null || settings == null || animator.runtimeAnimatorController == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerAnimationController] : 필수 애니메이션 참조가 사라졌습니다.");
#endif
            enabled = false;
            return;
        }

        if (!animator.isActiveAndEnabled)
        {
            return;
        }

        // 첫 재생은 이동 방향을 즉시 반영하고, 이후에는 설정된 시간으로 방향 변화를 부드럽게 완화합니다.
        float damping = hasPlayedState ? settings.BlendDuration : 0f;
        animator.SetFloat(moveXHash, moveDirection.x, damping, Time.deltaTime);
        animator.SetFloat(moveYHash, moveDirection.y, damping, Time.deltaTime);
        if (!shouldChangeState)
        {
            return;
        }

        shouldChangeState = false;
        if (hasPlayedState && requestedState == playedState)
        {
            return;
        }

        int stateHash;
        switch (requestedState)
        {
            case PlayerState.Idle:
                stateHash = idleHash;
                break;
            case PlayerState.Walk:
                stateHash = walkHash;
                break;
            case PlayerState.Run:
                stateHash = runHash;
                break;
            case PlayerState.CrouchIdle:
                stateHash = crouchIdleHash;
                break;
            case PlayerState.CrouchWalk:
                stateHash = crouchWalkHash;
                break;
            case PlayerState.Jump:
                stateHash = jumpHash;
                break;
            case PlayerState.Fall:
                stateHash = fallHash;
                break;
            case PlayerState.Dead:
                stateHash = deadHash;
                break;
            default:
#if UNITY_EDITOR
                Debug.LogError("[PlayerAnimationController] : 현재 HFSM 상태에 해당하는 Animator 상태가 없습니다.");
#endif
                enabled = false;
                return;
        }

        // HasState(int layerIndex, int stateID) : 플레이어는 baseLayer만 사용할 예정이므로, 0를 넣는다.
        if (!animator.HasState(0, stateHash))
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerAnimationController] : 현재 HFSM 상태에 해당하는 Animator 상태가 없습니다.");
#endif
            enabled = false;
            return;
        }

        if (hasPlayedState && settings.BlendDuration > 0f)
        {
            animator.CrossFadeInFixedTime(stateHash, settings.BlendDuration, 0, 0f);
        }
        else
        {
            animator.Play(stateHash, 0, 0f);
        }

        playedState = requestedState;
        hasPlayedState = true;
    }

    /// <summary>
    /// 재생 예약을 초기화합니다. Animator 설정과 전달받은 요청값은 유지합니다.
    /// </summary>
    private void OnDisable()
    {
        hasPlayedState = false;
        shouldChangeState = false;
    }

    /// <summary>
    /// 재생할 상태를 예약합니다. 같은 상태 요청은 재시작하지 않으며 비활성 중에도 최신 요청을 보관합니다.
    /// </summary>
    public void SetState(PlayerState state)
    {
        if (requestedState == state)
        {
            return;
        }

        requestedState = state;
        shouldChangeState = true;
    }

    /// <summary>
    /// 혼합할 이동 방향을 보관합니다. 상태 재진입 없이 MoveX/MoveY만 갱신합니다.
    /// </summary>
    public void SetMoveDirection(Vector2 direction)
    {
        moveDirection = direction;
    }
}
