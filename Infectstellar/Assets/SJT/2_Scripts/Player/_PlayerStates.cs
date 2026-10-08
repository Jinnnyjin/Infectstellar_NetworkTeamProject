using UnityEngine;
using UnityHFSM;

/// <summary>
/// 생존·지상·공중 계층의 전환과 상태별 이동 명령을 결정합니다. 물리를 실행하지 않습니다.
/// </summary>
internal sealed class _PlayerStates
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
    private PlayerMovementSettings settings;
    private PlayerInputFrame input;
    private bool isMoving;
    private bool shouldCrouch;
    private bool bodyCrouching;
    private bool isGrounded = true;
    private bool jumpAccepted;
    private bool jumpCommand;
    private bool crouchCommand;
    private float verticalVelocity;
    private float movementSpeed;
    private float airborneSpeed;

    /// <summary>
    /// 활성 계층의 최종 상태를 반환합니다.
    /// </summary>
    internal PlayerState CurrentState => !IsAlive ? PlayerState.Dead : alive.ActiveStateName == MovementState.Grounded ? grounded.ActiveStateName : airborne.ActiveStateName;

    /// <summary>
    /// 생존 계층이 활성 상태인지 반환합니다.
    /// </summary>
    internal bool IsAlive => root.ActiveStateName == LifeState.Alive;

    /// <summary>
    /// 현재 상태가 선택한 캡슐 자세를 반환합니다.
    /// </summary>
    internal bool WantsCrouch => crouchCommand;

    /// <summary>
    /// 상태 그래프를 구성하고 Alive/Grounded/Idle로 초기화합니다.
    /// </summary>
    internal _PlayerStates()
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
        alive.AddTransition(MovementState.Grounded, MovementState.Airborne, transition => !isGrounded || jumpAccepted, onTransition: transition => CaptureAirborneSpeed());
        alive.AddTransition(MovementState.Airborne, MovementState.Grounded, transition => isGrounded && !jumpAccepted);

        root.AddState(LifeState.Alive, alive);
        root.AddState(LifeState.Dead, onEnter: state => ApplyDeadPolicy(), onLogic: state => ApplyDeadPolicy());
        root.SetStartState(LifeState.Alive);
        root.AddTriggerTransition(LifeEvent.Die, LifeState.Alive, LifeState.Dead, forceInstantly: true);
        root.AddTriggerTransition(LifeEvent.Revive, LifeState.Dead, LifeState.Alive, forceInstantly: true);
        root.Init();
    }

    /// <summary>
    /// 공통 이동 설정을 연결합니다. 그래프를 다시 생성하지 않습니다.
    /// </summary>
    internal void Configure(PlayerMovementSettings settings)
    {
        this.settings = settings;
        airborneSpeed = settings.WalkSpeed;
    }

    /// <summary>
    /// 입력과 현재 물리 상황에서 한 번 실행할 이동 명령을 생성합니다.
    /// </summary>
    internal PlayerMovementCommand Prepare(PlayerInputFrame input, bool isGrounded, float verticalVelocity, bool isCrouching, bool canStand)
    {
        this.input = input;
        isMoving = input.Move.sqrMagnitude > 0.0001f;
        SetContext(isGrounded, verticalVelocity, isCrouching, canStand);
        jumpAccepted = IsAlive && isGrounded && !isCrouching && !shouldCrouch && input.JumpPressed && settings.JumpHeight > 0f;
        jumpCommand = false;
        root.OnLogic();
        return new PlayerMovementCommand(IsAlive ? input.Move : Vector2.zero, movementSpeed, jumpCommand, crouchCommand);
    }

    /// <summary>
    /// 이동 결과로 상태와 자세만 보정합니다. 이동이나 점프를 다시 실행하지 않습니다.
    /// </summary>
    internal void Complete(bool isGrounded, float verticalVelocity, bool isCrouching, bool canStand)
    {
        jumpAccepted = false;
        jumpCommand = false;
        SetContext(isGrounded, verticalVelocity, isCrouching, canStand);
        root.OnLogic();
    }

    /// <summary>
    /// 모든 생존 상태에서 즉시 사망하고 새 점프 명령을 버립니다.
    /// </summary>
    internal void Die()
    {
        if (IsAlive)
        {
            jumpAccepted = false;
            jumpCommand = false;
            root.Trigger(LifeEvent.Die);
        }
    }

    /// <summary>
    /// 사망 중에만 입력을 비우고 생존 계층으로 복귀합니다. 다음 갱신에 실제 접지를 반영합니다.
    /// </summary>
    internal void Revive()
    {
        if (!IsAlive)
        {
            input = default;
            isMoving = false;
            shouldCrouch = bodyCrouching;
            jumpAccepted = false;
            jumpCommand = false;
            root.Trigger(LifeEvent.Revive);
        }
    }

    /// <summary>
    /// 파괴 시 활성 하위 상태까지 종료합니다.
    /// </summary>
    internal void Exit()
    {
        root.OnExit();
    }

    /// <summary>
    /// 물리 결과와 일어설 공간을 상태 전환 조건으로 보관합니다.
    /// </summary>
    private void SetContext(bool isGrounded, float verticalVelocity, bool isCrouching, bool canStand)
    {
        this.isGrounded = isGrounded;
        this.verticalVelocity = verticalVelocity;
        bodyCrouching = isCrouching;
        shouldCrouch = input.CrouchHeld || (isCrouching && !canStand);
    }

    /// <summary>
    /// 이륙 순간의 자세·달리기 의도에서 공중 이동 속도 기준을 보관합니다.
    /// </summary>
    private void CaptureAirborneSpeed()
    {
        airborneSpeed = bodyCrouching ? settings.CrouchSpeed : input.RunHeld && isMoving ? settings.RunSpeed : settings.WalkSpeed;
    }

    /// <summary>
    /// 지상 상태별 속도와 웅크림 정책을 등록합니다.
    /// </summary>
    private void ConfigureGroundedStates()
    {
        grounded.AddState(PlayerState.Idle, onEnter: state => ApplyGroundedPolicy(0f, false), onLogic: state => ApplyGroundedPolicy(0f, false));
        grounded.AddState(PlayerState.Walk, onEnter: state => ApplyGroundedPolicy(settings.WalkSpeed, false), onLogic: state => ApplyGroundedPolicy(settings.WalkSpeed, false));
        grounded.AddState(PlayerState.Run, onEnter: state => ApplyGroundedPolicy(settings.RunSpeed, false), onLogic: state => ApplyGroundedPolicy(settings.RunSpeed, false));
        grounded.AddState(PlayerState.CrouchIdle, onEnter: state => ApplyGroundedPolicy(0f, true), onLogic: state => ApplyGroundedPolicy(0f, true));
        grounded.AddState(PlayerState.CrouchWalk, onEnter: state => ApplyGroundedPolicy(settings.CrouchSpeed, true), onLogic: state => ApplyGroundedPolicy(settings.CrouchSpeed, true));
        grounded.SetStartState(PlayerState.Idle);

        grounded.AddTransitionFromAny(PlayerState.Idle, transition => !isMoving && !shouldCrouch);
        grounded.AddTransitionFromAny(PlayerState.Walk, transition => isMoving && !input.RunHeld && !shouldCrouch);
        grounded.AddTransitionFromAny(PlayerState.Run, transition => isMoving && input.RunHeld && !shouldCrouch);
        grounded.AddTransitionFromAny(PlayerState.CrouchIdle, transition => !isMoving && shouldCrouch);
        grounded.AddTransitionFromAny(PlayerState.CrouchWalk, transition => isMoving && shouldCrouch);
    }

    /// <summary>
    /// 공중 상태를 등록하며 승인한 점프만 진입 시 한 번 명령으로 만듭니다.
    /// </summary>
    private void ConfigureAirborneStates()
    {
        airborne.AddState(PlayerState.Jump, onEnter: state => EnterJump(), onLogic: state => ApplyAirbornePolicy());
        airborne.AddState(PlayerState.Fall, onEnter: state => ApplyAirbornePolicy(), onLogic: state => ApplyAirbornePolicy());
        airborne.SetStartState(PlayerState.Fall);
        airborne.AddTransitionFromAny(PlayerState.Jump, transition => jumpAccepted || verticalVelocity > 0f);
        airborne.AddTransitionFromAny(PlayerState.Fall, transition => !jumpAccepted && verticalVelocity <= 0f);
    }

    /// <summary>
    /// 지상 상태가 허용한 속도와 캡슐 자세를 선택합니다.
    /// </summary>
    private void ApplyGroundedPolicy(float speed, bool crouch)
    {
        movementSpeed = speed;
        crouchCommand = crouch;
    }

    /// <summary>
    /// 공중에서는 이륙 속도와 실제 캡슐 자세를 유지합니다.
    /// </summary>
    private void ApplyAirbornePolicy()
    {
        movementSpeed = airborneSpeed;
        crouchCommand = bodyCrouching;
    }

    /// <summary>
    /// 상승 판정과 실제 점프 요청을 구분해 중복 점프를 방지합니다.
    /// </summary>
    private void EnterJump()
    {
        jumpCommand = jumpAccepted;
        ApplyAirbornePolicy();
    }

    /// <summary>
    /// 사망 중 수평 이동과 점프를 차단하고 현재 캡슐 자세를 유지합니다.
    /// </summary>
    private void ApplyDeadPolicy()
    {
        movementSpeed = 0f;
        jumpCommand = false;
        crouchCommand = bodyCrouching;
    }
}
