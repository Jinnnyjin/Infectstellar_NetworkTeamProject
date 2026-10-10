using UnityEngine;
using UnityHFSM;
using StateMachine = UnityHFSM.StateMachine;

sealed class PlayerStateGraph
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
    /// TEvent : 트리거를 식별하는 값
    /// </summary>
    private readonly StateMachine<LifeState, LifeEvent> root;
    private readonly StateMachine<LifeState, MovementState, LifeEvent> alive;
    private readonly StateMachine<MovementState, PlayerState, LifeEvent> grounded;
    private readonly StateMachine<MovementState, PlayerState, LifeEvent> airborne;
    private PlayerMovementSettings settings; // 플레이어가 이동, 점프 할 때의 설정값
    private PlayerInputFrame input; // 이번 프레임에 전달받은 입력을 저장해서, 상태 전환 조건과 행동 메서드에서 함께 사용하기 위한 필드
    #region 웅크리기 관련 필드
    private bool shouldCrouch; // 입력과 일어서기 가능 여부를 고려해 웅크려야 하는지 나타냄
    private bool bodyCrouching; // Motor의 실제 충돌 캡슐이 웅크린 상태인지 나타냄
    private bool crouchCommand; // 상태 판단 후 Motor에 요청할 웅크림 여부
    #endregion
    #region 점프 관련 필드
    private bool jumpAccepted; // 이번 프레임의 점프 입력이 허용 조건을 만족했는지 나타냄
    private bool jumpCommand; // Motor에 이번 프레임의 점프 실행을 요청할지 나타냄
    #endregion
    #region 움직임 관련 필드
    private bool isMoving; // 이동 입력이 있는지 여부. 실제 범위와는 무관
    private float verticalVelocity; // Motor에서 전달받은 수직 속도. 위쪽이 양수이며 단위는 m/s
    private float movementSpeed; // 현재 상태가 선택한 수평 이동 속도. 단위는 m/s
    #endregion
    #region 플레이어 접지 관련 필드
    private float airbornesSpeed; // 공중에서 사용할 수평 이동 속도
    private bool isGrounded = true; // Motor에서 전달받은 현재 접지 여부
    #endregion

    // 활성 계층의 최종 상태를 반환합니다.
    internal PlayerState CurrentState => !IsAlive ? PlayerState.Dead : alive.ActiveStateName == MovementState.Grounded ? grounded.ActiveStateName : airborne.ActiveStateName;

    // 생존 상태인지 (생존 계층이 활성 상태인지 반환 )
    public bool IsAlive => root.ActiveStateName == LifeState.Alive;

    // 현재 상태가 선택한 캡슐 자세를 반환
    public bool WantsCrouch => crouchCommand;

    /// <summary>
    /// 상태 그래프를 구성하고 Alive/Grounded/Idle로 초기화합니다.
    /// </summary>
    public PlayerStateGraph()
    {
        root = new StateMachine<LifeState, LifeEvent>(); // 살아있음 / 죽음 관리 상태머신
        alive = new StateMachine<LifeState, MovementState, LifeEvent>(); // 살아있는 동안의 상태머신
        grounded = new StateMachine<MovementState, PlayerState, LifeEvent>(); // 땅 위에 있는 동안의 상태머신
        airborne = new StateMachine<MovementState, PlayerState, LifeEvent>(); // 공중에 떠 있는 동안의 상태머신
        ConfigureGroundedStates();
        ConfigureAirborneStates();

        // AddState 첫 번째 인자(name): 해당 FSM 안에서 사용할 상태 이름
        // AddState 두 번째 인자(onEnter): 상태에 들어갈 때 실행할 함수
        // AddState 세 번째 인자(onLogic): 활성 상태에서 OnLogic()이 호출될 때 실행할 함수
        // AddState 네 번째 인자(onExit): 상태에서 나갈 때 실행할 함수
        // AddState 다섯 번째 인자(canExit): 종료 대기 중, true이면 나가도 된다고 판단하는 함수
        // AddState 여섯 번째 인자(needsExitTime): true이면 상태를 나가기 전에 종료 가능 여부를 기다린다.
        // AddState 일곱 번째 인자(isGhostState): true이면 상태에 진입하자마자 다음 전환 조건을 확인한다.
        alive.AddState(MovementState.Grounded, grounded);
        alive.AddState(MovementState.Airborne, airborne);
        alive.SetStartState(MovementState.Grounded);

        // AddTransition 첫 번째 인자(from): 전환하기 전의 상태 이름
        // AddTransition 두 번째 인자(to): 전환해서 들어갈 상태 이름
        // AddTransition 세 번째 인자(condition): true이면 전환을 허용하는 조건 함수
        // AddTransition 네 번째 인자(onTransition): 실제 상태 전환 직전에 실행할 함수
        // AddTransition 다섯 번째 인자(afterTransition): 실제 상태 전환 직후에 실행할 함수
        // AddTransition 여섯 번째 인자(forceInstantly): true이면 종료 대기 없이 전환한다.

        // 이륙 순간의 이동 속도를 저장해 공중에서도 같은 속도 기준을 유지한다.
        alive.AddTransition(MovementState.Grounded, MovementState.Airborne, transition => !isGrounded || jumpAccepted, onTransition: transition => CaptureAirborneSpeed());
        // 착지 후 이동 속도는 지상 상태의 행동에서 다시 결정한다.
        alive.AddTransition(MovementState.Airborne, MovementState.Grounded, transition => isGrounded && !jumpAccepted);

        root.AddState(LifeState.Alive, alive);
        root.AddState(LifeState.Dead, onEnter: state => ApplyDeadPolicy(), onLogic: state => ApplyDeadPolicy());
        root.SetStartState(LifeState.Alive);

        root.AddTriggerTransition(LifeEvent.Die, LifeState.Alive, LifeState.Dead, forceInstantly: true);
        root.AddTriggerTransition(LifeEvent.Revive, LifeState.Dead, LifeState.Alive, forceInstantly: true);
        root.Init(); // 등록한 시작 상태 Alive/Grounded/Idle에 실제로 진입합니다.
    }

    /// <summary>
    /// 입력과 현재 물리 상황에서 한 번 실행할 이동 명령을 생성합니다.
    /// </summary>
    /// <param name="_input">현재 어떤 인풋이 들어왔는지</param>
    /// <param name="isGrounded">플레이어가 땅과 접지해있는지</param>
    /// <param name="verticalVelocity">수직 속도 (점프중인지 낙하중인지)</param>
    /// <param name="isCrouching">수구리고 있는 상황인지</param>
    /// <param name="canStand">일어날 수 있는 상황인지</param>
    /// <returns></returns>
    /// 
    /// StateMachine.OnLogic()
    /// 상태 전환 조건을 확인하고, 현재 활성 상태에 등록된 행동(onEnter, onLogic, onExit)을 실행한다.
    public PlayerMovementCommand CreateMovementCommand(PlayerInputFrame _input, bool isGrounded, float verticalVelocity, bool isCrouching, bool canStand)
    {
        input = _input;
        isMoving = input.Move.sqrMagnitude > 0.0001f;
        SetContext(isGrounded, verticalVelocity, isCrouching, canStand);
        jumpAccepted = IsAlive && isGrounded && !isCrouching && !shouldCrouch && input.JumpPressed && settings.JumpHeight > 0f;
        jumpCommand = false;
        root.OnLogic();
        return new PlayerMovementCommand(IsAlive ? input.Move : Vector2.zero, movementSpeed, jumpCommand, crouchCommand);
    }

    /// 실제 이동 후의 몸 정보를 반영해, 착지·낙하 등 현재 상태를 갱신한다.
    public void UpdateStateAfterMovement(bool isGrounded, float verticalVelocity, bool isCrouching, bool canStand)
    {
        jumpAccepted = false;
        jumpCommand = false;
        SetContext(isGrounded, verticalVelocity, isCrouching, canStand);
        root.OnLogic();
    }

    /// 모든 생존 상태에서 즉시 사망하고 새 점프 명령을 버립니다.
    public void Die()
    {
        if (IsAlive)
        {
            jumpAccepted = false;
            jumpCommand = false;
            root.Trigger(LifeEvent.Die);
        }
    }

    public void Revive()
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

    /// 파괴 시 활성 하위 상태까지 종료합니다.
    public void Exit()
    {
        root.OnExit();
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
