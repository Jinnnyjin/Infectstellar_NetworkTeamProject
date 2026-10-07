using UnityEngine;
using UnityHFSM;
using StateMachine = UnityHFSM.StateMachine;

sealed class PlayerStates
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

    /// <summary>
    /// StateMachine에 제너릭 타입 인수를 넣어줌으로써 상태머신의 실제 타입을 정해줌
    /// StateMachine의 기본 형태 : StateMachine<TOwnId, TStateId, TEvent>, 타입이 2개인 경우 <TStateId, TEvent>
    /// TOwnId : 부모에서 나에게 붙일 이름의 타입
    /// TStateId : 내 안에 등록할 상태 이름의 타입
    /// 부모의 TStateId와 자식의 TOwnId 타입이 일치해야 자식 FSM을 상태로 등록 가능
    /// TEvent : 사용할 사건 이름의 타입
    /// </summary>
    private readonly StateMachine<LifeState, LifeEvent> root;
    private readonly StateMachine<LifeState, MovementState, LifeEvent> alive;
    private readonly StateMachine<MovementState, PlayerState, LifeEvent> grounded;
    private readonly StateMachine<MovementState, PlayerState, LifeEvent> airborne;
    private PlayerMovementSettings settings; // 플레이어가 이동, 점프 할 때의 설정값
    private PlayerInputFrame input;
    private bool isMoving; // 이동 입력이 있는지 여부. 실제 범위와는 무관
    private bool shouldCrouch; // 입력과 일어서기 가능 여부를 고려해 웅크려야 하는지 나타냄
    private bool bodyCrouching; // Motor의 실제 충돌 캡슐이 웅크린 상태인지 나타냄
    private bool isGrounded = true; // Motor에서 전달받은 현재 접지 여부
    private bool jumpAccepted; // 이번 프레임의 점프 입력이 허용 조건을 만족했는지 나타냄
    private bool jumpCommand; // Motor에 이번 프레임의 점프 실행을 요청할지 나타냄
    private bool crouchCommand; // 상태 판단 후 Motor에 요청할 웅크림 여부
    private float verticalVelocity; // Motor에서 전달받은 수직 속도. 위쪽이 양수이며 단위는 m/s
    private float movementSpeed; // 현재 상태가 선택한 수평 이동 속도. 단위는 m/s
    private float airbornesSpeed; // 공중에서 사용할 수평 이동 속도

    public PlayerStates()
    {
        root = new StateMachine<LifeState, LifeEvent>();
        alive = new StateMachine<LifeState, MovementState, LifeEvent>();
        grounded = new StateMachine<MovementState, PlayerState, LifeEvent>();
        airborne = new StateMachine<MovementState, PlayerState, LifeEvent>();
        ConfigureGroundedStates();
        ConfigureAirborneStates();

        // AddState 첫 번째 인자 : 해당 FSM 안에서 사용할 이름표
        // grounded : 그 이름표로 등록할 FSM 객체
        alive.AddState(MovementState.Grounded, grounded);
        alive.AddState(MovementState.Airborne, airborne);
        alive.SetStartState(MovementState.Grounded);

        alive.AddTransition(MovementState.Grounded, MovementState.Airborne, transition => !isGrounded || jumpAccepted, onTransition: transition => CaptureAirborneSpeed());
        alive.AddTransition(MovementState.Airborne, MovementState.Grounded, transition => isGrounded && !jumpAccepted);

    }

    /// <summary>
    /// 공통 이동 설정을 연결합니다.
    /// </summary>
    /// <param name="_settings">플레이어의 움직임 관련 데이터</param>
    public void Configure(PlayerMovementSettings _settings)
    {
        settings = _settings;
        airbornesSpeed = settings.WalkSpeed;
    }

    /// <summary>
    /// 물리 결과와 일어설 공간을 상태 전환 조건으로 보관합니다.
    /// </summary>
    /// <param name="_isGrounded">플레이어가 땅에 붙어 있는 상태인지</param>
    /// <param name="_verticalvelocity">Motor에서 전달하는 수직 속도. 양수면 위쪽, 음수면 아랫쪽</param>
    /// <param name="isCrouching">웅크리고 있는지 여부</param>
    /// <param name="canStand">외부에서 전달받은 일어날 수 있는 상태 여부. 물리적으로 일어날 수 있는지를 전달받는다.</param>
    private void SetContext(bool _isGrounded, float _verticalvelocity, bool isCrouching, bool canStand)
    {
        isGrounded = _isGrounded;
        verticalVelocity = _verticalvelocity;
        bodyCrouching = isCrouching;
        shouldCrouch = input.CrouchHeld || (isCrouching && !canStand);
    }

    #region 상태별 이동 명령 설정
    private void ApplyGroundedPolicy(float speed, bool crouch)
    {
        movementSpeed = speed;
        crouchCommand = crouch;
    }

    private void ApplyAirbornePolicy()
    {
        movementSpeed = airbornesSpeed;
        // crouchCommand = bodyCrouching; 는 “현재 자세를 유지해라”
        crouchCommand = bodyCrouching;
    }

    private void ApplyDeadPolicy()
    {
        movementSpeed = 0f;
        jumpCommand = false;
        crouchCommand = bodyCrouching;
    }
    #endregion

    /// <summary>
    /// 이륙 순간의 자세·달리기 의도에서 공중 이동 속도 기준을 보관합니다.
    /// </summary>
    private void CaptureAirborneSpeed()
    {
        airbornesSpeed = bodyCrouching ? settings.CrouchSpeed : input.RunHeld && isMoving ? settings.RunSpeed : settings.WalkSpeed;
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

    private void EnterJump()
    {
        jumpCommand = jumpAccepted;
        ApplyAirbornePolicy(); // 점프시 점프 이동속도 적용
    }
}