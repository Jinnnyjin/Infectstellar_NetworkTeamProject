using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HFSM의 상태 변경과 이동 입력을 Animator에 전달합니다. 재생과 혼합은 Animator가 처리합니다.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(PlayerStateMachine), typeof(PlayerInputReader))]
public sealed class PlayerAnimationController : MonoBehaviour
{
    private static readonly int moveXHash = Animator.StringToHash("MoveX");
    private static readonly int moveYHash = Animator.StringToHash("MoveY");
    private static readonly int[] stateHashes =
    {
        Animator.StringToHash("Base Layer.Idle"),
        Animator.StringToHash("Base Layer.Walk"),
        Animator.StringToHash("Base Layer.Run"),
        Animator.StringToHash("Base Layer.CrouchIdle"),
        Animator.StringToHash("Base Layer.CrouchWalk"),
        Animator.StringToHash("Base Layer.Jump"),
        Animator.StringToHash("Base Layer.Fall"),
        Animator.StringToHash("Base Layer.Dead")
    };

    [SerializeField, Tooltip("클립 교체는 시작 또는 재활성화 때 반영합니다.")] private PlayerAnimationSettings settings;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerStateMachine stateMachine;
    [SerializeField] private PlayerInputReader inputReader;

    private AnimatorOverrideController overrideController;
    private RuntimeAnimatorController previousController;
    private PlayerStateMachine subscribedStateMachine;
    private PlayerState requestedState;
    private bool previousRootMotion;
    private bool hasPlayedState;
    private bool shouldChangeState;

    /// <summary>
    /// 플레이어별 클립 교체본을 준비하고 현재 HFSM 상태부터 재생하도록 예약합니다.
    /// </summary>
    private void OnEnable()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (stateMachine == null)
        {
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (settings == null || animator == null || stateMachine == null || inputReader == null
            || settings.BaseController == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman
            || animator.gameObject != gameObject || stateMachine.gameObject != gameObject || inputReader.gameObject != gameObject)
        {
            Fail("같은 Player의 Settings, Animator, StateMachine, InputReader와 유효한 Humanoid Avatar, 기본 Controller가 필요합니다.");
            return;
        }

        AnimationClip idle = settings.GetClip("idle");
        if (idle == null || idle.legacy || !idle.humanMotion || !idle.isLooping)
        {
            Fail("기본 Idle에는 반복 재생하는 Humanoid 클립을 연결해야 합니다.");
            return;
        }

        overrideController = new AnimatorOverrideController(settings.BaseController);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
        overrideController.GetOverrides(overrides);
        if (overrides.Count != 17)
        {
            Fail("기본 Controller에는 서로 다른 17개 슬롯 클립이 필요합니다.");
            return;
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip slot = overrides[i].Key;
            AnimationClip clip = settings.GetClip(slot.name);
            if (clip == null)
            {
                Fail("기본 Controller의 슬롯 이름을 확인해야 합니다.");
                return;
            }

            if (clip.legacy || !clip.humanMotion)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[PlayerAnimationController] {slot.name}의 클립이 Humanoid가 아니어서 Idle로 대체합니다.", this);
#endif
                clip = idle;
            }

#if UNITY_EDITOR
            bool shouldLoop = slot.name != "jump" && slot.name != "fall" && slot.name != "death";
            if (clip != idle && clip.isLooping != shouldLoop)
            {
                Debug.LogWarning($"[PlayerAnimationController] {slot.name}의 Loop Time을 {(shouldLoop ? "켜야" : "꺼야")} 합니다.", this);
            }
#endif
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(slot, clip);
        }

        overrideController.ApplyOverrides(overrides);
        previousController = animator.runtimeAnimatorController;
        previousRootMotion = animator.applyRootMotion;
        animator.runtimeAnimatorController = overrideController;
        animator.applyRootMotion = false;
        subscribedStateMachine = stateMachine;
        subscribedStateMachine.StateChanged += HandleStateChanged;
        requestedState = stateMachine.CurrentState;
        shouldChangeState = true;
        hasPlayedState = false;
    }

    /// <summary>
    /// PlayerController의 입력/HFSM 갱신 뒤, Animator 평가 전에 방향과 예약된 상태를 전달합니다.
    /// </summary>
    private void Update()
    {
        if (animator == null || stateMachine == null || inputReader == null || settings == null)
        {
            Fail("필수 애니메이션 참조가 사라졌습니다.");
            return;
        }

        if (!animator.isActiveAndEnabled || overrideController == null)
        {
            return;
        }

        Vector2 move = inputReader.isActiveAndEnabled ? inputReader.CurrentFrame.Move : Vector2.zero;
        float damping = hasPlayedState ? settings.BlendDuration : 0f;
        animator.SetFloat(moveXHash, move.x, damping, Time.deltaTime);
        animator.SetFloat(moveYHash, move.y, damping, Time.deltaTime);
        if (!shouldChangeState)
        {
            return;
        }

        int index = (int)requestedState;
        if (index < 0 || index >= stateHashes.Length || !animator.HasState(0, stateHashes[index]))
        {
            Fail("현재 HFSM 상태에 해당하는 Animator 상태가 없습니다.");
            return;
        }

        if (hasPlayedState && settings.BlendDuration > 0f)
        {
            animator.CrossFadeInFixedTime(stateHashes[index], settings.BlendDuration, 0, 0f);
        }
        else
        {
            animator.Play(stateHashes[index], 0, 0f);
        }

        hasPlayedState = true;
        shouldChangeState = false;
    }

    /// <summary>
    /// 구독과 플레이어별 교체본을 정리하고 기존 Animator 설정을 복구합니다.
    /// </summary>
    private void OnDisable()
    {
        if (subscribedStateMachine != null)
        {
            subscribedStateMachine.StateChanged -= HandleStateChanged;
        }

        subscribedStateMachine = null;
        if (overrideController != null)
        {
            if (animator != null && animator.runtimeAnimatorController == overrideController)
            {
                animator.runtimeAnimatorController = previousController;
                animator.applyRootMotion = previousRootMotion;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(overrideController);
            }
            else
#endif
            {
                Destroy(overrideController);
            }
            overrideController = null;
        }

        previousController = null;
        hasPlayedState = false;
        shouldChangeState = false;
    }

    /// <summary>
    /// 같은 프레임에 여러 번 변경되어도 최종 상태를 Animator 평가 전에 한 번 적용합니다.
    /// </summary>
    private void HandleStateChanged(PlayerState previous, PlayerState current)
    {
        requestedState = current;
        shouldChangeState = true;
    }

    /// <summary>
    /// 잘못된 연결은 애니메이션만 중지하고 에디터에서 원인을 알립니다.
    /// </summary>
    private void Fail(string message)
    {
#if UNITY_EDITOR
        Debug.LogError($"[PlayerAnimationController] {message}", this);
#endif
        enabled = false;
    }
}
