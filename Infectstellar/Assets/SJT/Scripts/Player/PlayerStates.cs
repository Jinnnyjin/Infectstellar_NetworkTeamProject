using UnityHFSM;

/// <summary>
/// 생존, 지상, 공중 상태의 계층과 전환 조건을 관리합니다.
/// </summary>
internal sealed class PlayerStates
{
    private enum LifeState
    {
        Alive,
        Dead
    }

    private enum MovementState
    {
        Grounded,
        Airborne
    }

    private enum LifeEvent
    {
        Die,
        Revive
    }

    private readonly StateMachine<LifeState, LifeEvent> root;
    private readonly StateMachine<LifeState, MovementState, LifeEvent> alive;
    private readonly StateMachine<MovementState, PlayerState, LifeEvent> grounded;
    private readonly StateMachine<MovementState, PlayerState, LifeEvent> airborne;
    private bool isMoving;
    private bool wantsToRun;
    private bool wantsToCrouch;
    private bool isGrounded = true;
    private float verticalVelocity;

    /// <summary>
    /// 활성 계층의 최종 상태를 반환하며, 사망 시 종료된 하위 계층을 조회하지 않습니다.
    /// </summary>
    internal PlayerState CurrentState
    {
        get
        {
            if (!IsAlive)
            {
                return PlayerState.Dead;
            }

            return alive.ActiveStateName == MovementState.Grounded ? grounded.ActiveStateName : airborne.ActiveStateName;
        }
    }

    /// <summary>
    /// 생존 계층이 활성 상태인지 반환합니다.
    /// </summary>
    internal bool IsAlive => root.ActiveStateName == LifeState.Alive;

    /// <summary>
    /// 상태 그래프를 한 번 구성하고 Alive/Grounded/Idle로 초기화합니다.
    /// </summary>
    internal PlayerStates()
    {
        root = new StateMachine<LifeState, LifeEvent>();
        alive = new StateMachine<LifeState, MovementState, LifeEvent>();
        grounded = new StateMachine<MovementState, PlayerState, LifeEvent>();
        airborne = new StateMachine<MovementState, PlayerState, LifeEvent>();

        ConfigureGroundedStates();
        ConfigureAirborneStates();

        alive.AddState(MovementState.Grounded, grounded);
        alive.AddState(MovementState.Airborne, airborne);
        alive.SetStartState(MovementState.Grounded);
        alive.AddTransition(MovementState.Grounded, MovementState.Airborne, transition => !isGrounded);
        alive.AddTransition(MovementState.Airborne, MovementState.Grounded, transition => isGrounded);

        root.AddState(LifeState.Alive, alive);
        root.AddState(LifeState.Dead);
        root.SetStartState(LifeState.Alive);
        root.AddTriggerTransition(LifeEvent.Die, LifeState.Alive, LifeState.Dead, forceInstantly: true);
        root.AddTriggerTransition(LifeEvent.Revive, LifeState.Dead, LifeState.Alive, forceInstantly: true);
        root.Init();
    }

    /// <summary>
    /// 외부에서 전달한 이동 상황으로 활성 계층 전체를 즉시 갱신합니다.
    /// </summary>
    internal void UpdateMovementContext(bool isMoving, bool wantsToRun, bool wantsToCrouch, bool isGrounded, float verticalVelocity)
    {
        if (!IsAlive)
        {
            return;
        }

        this.isMoving = isMoving;
        this.wantsToRun = wantsToRun;
        this.wantsToCrouch = wantsToCrouch;
        this.isGrounded = isGrounded;
        this.verticalVelocity = verticalVelocity;

        // 부모 전환 후 새 하위 계층의 OnLogic도 실행되어 이번 호출 안에 최종 상태가 결정됩니다.
        root.OnLogic();
    }

    /// <summary>
    /// 활성 생존 계층을 즉시 종료하고 사망 상태로 전환합니다.
    /// </summary>
    internal void Die()
    {
        if (!IsAlive)
        {
            return;
        }

        root.Trigger(LifeEvent.Die);
    }

    /// <summary>
    /// 사망한 경우에만 이전 이동 상황을 지우고 Idle로 부활합니다.
    /// </summary>
    internal void Revive()
    {
        if (IsAlive)
        {
            return;
        }

        isMoving = false;
        wantsToRun = false;
        wantsToCrouch = false;
        isGrounded = true;
        verticalVelocity = 0f;
        root.Trigger(LifeEvent.Revive);
    }

    /// <summary>
    /// 파괴 시 활성 하위 상태까지 종료합니다.
    /// </summary>
    internal void Exit()
    {
        root.OnExit();
    }

    /// <summary>
    /// 웅크리기 우선순위를 반영한 지상 상태와 전환을 구성합니다.
    /// </summary>
    private void ConfigureGroundedStates()
    {
        // 동작이 없는 상태는 라이브러리의 AddState가 가벼운 StateBase로 생성합니다.
        grounded.AddState(PlayerState.Idle);
        grounded.AddState(PlayerState.Walk);
        grounded.AddState(PlayerState.Run);
        grounded.AddState(PlayerState.CrouchIdle);
        grounded.AddState(PlayerState.CrouchWalk);
        grounded.SetStartState(PlayerState.Idle);

        grounded.AddTransitionFromAny(PlayerState.Idle, transition => !isMoving && !wantsToCrouch);
        grounded.AddTransitionFromAny(PlayerState.Walk, transition => isMoving && !wantsToRun && !wantsToCrouch);
        grounded.AddTransitionFromAny(PlayerState.Run, transition => isMoving && wantsToRun && !wantsToCrouch);
        grounded.AddTransitionFromAny(PlayerState.CrouchIdle, transition => !isMoving && wantsToCrouch);
        grounded.AddTransitionFromAny(PlayerState.CrouchWalk, transition => isMoving && wantsToCrouch);
    }

    /// <summary>
    /// 상승 중에는 Jump, 정점과 하강 중에는 Fall을 선택하는 공중 상태를 구성합니다.
    /// </summary>
    private void ConfigureAirborneStates()
    {
        airborne.AddState(PlayerState.Jump);
        airborne.AddState(PlayerState.Fall);
        airborne.SetStartState(PlayerState.Fall);

        airborne.AddTransitionFromAny(PlayerState.Jump, transition => verticalVelocity > 0f);
        airborne.AddTransitionFromAny(PlayerState.Fall, transition => verticalVelocity <= 0f);
    }
}
