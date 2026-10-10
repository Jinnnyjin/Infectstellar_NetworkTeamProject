using System;
using UnityEngine;

/// <summary>
/// 입력, 물리 상황을 HFSM에 전달하고 한 번 이동한 뒤 최종 상태의 변경을 알림.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStateMachine : MonoBehaviour
{
    private PlayerStateGraph stateGraph;
    private PlayerState currentState = PlayerState.Idle;
    private PlayerMotor motor;

    // 마지막으로 확정한 최종 상태를 반환
    public PlayerState CurrentState => currentState;

    // 플레이어가 사망 상태가 아닌지 반환
    public bool IsAlive => currentState != PlayerState.Dead;

    // 최종 상태가 바뀐 뒤 변경 전과 변경 후의 상태를 한 번만 알림
    public event Action<PlayerState, PlayerState> StateChanged;

    // 그래프를 종료하고 보관한 이벤트 구독 참조 해제
    private void OnDestroy()
    {
        stateGraph?.Exit();
        stateGraph = null;
        StateChanged = null;
    }

    /// <summary>
    /// Controller 쪽에서 호출하며 그래프를 준비하고 Motor와 이동 설정을 연결
    /// </summary>
    /// <param name="motor">Player의 움직임을 담당하는 컴포넌트</param>
    /// <param name="settings">Player의 움직임 관련 데이터 SO</param>
    public void Initialize(PlayerMotor motor, PlayerMovementSettings settings)
    {
        stateGraph ??= new PlayerStateGraph();

        this.motor = motor;
        stateGraph.Configure(settings);
    }

    /// <summary>
    /// 이동 전 명령 생성 → 한 번의 물리 이동 실행 → 이동 후 상태 보정 을 순서대로 진행
    /// </summary>
    /// <param name="input"></param>
    /// <param name="deltaTime"></param>
    public void Tick(PlayerInputFrame input, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        // Graph에 전달하는 motor.IsGrounded가 현재 위치를 검사한 결과가 되도록 먼저 실행
        motor.RefreshGrounded();
        // 이동 전 입력과 몸 정보를 Graph에 전달해서 이동 명령을 받음
        PlayerMovementCommand command = stateGraph.CreateMovementCommand(input, motor.IsGrounded, motor.VerticalVelocity, motor.IsCrouching, motor.CanStand());
        // 만들어진 명령으로 몸을 실제로 움직이며 한 번의 물리 이동 실행
        motor.Simulate(command, deltaTime);
        // 이동해서 바뀐 몸 정보를 Graph에 다시 전달
        stateGraph.UpdateStateAfterMovement(motor.IsGrounded, motor.VerticalVelocity, motor.IsCrouching, motor.CanStand());
        // 이동 후 갱신된 웅크림 판단을 실제 캡슐 자세에 반영
        motor.SetCrouching(stateGraph.WantsCrouch);
        // 최종 상태가 바뀌었으면 현재 상태를 갱신하고 StateChanged 이벤트로 알림
        PublishStateChange();
    }

    /// <summary>
    /// 모든 생존 상태에서 사망 상태로 즉시 전환
    /// </summary>
    public void Die()
    {
        stateGraph.Die();
        PublishStateChange();
    }

    /// <summary>
    /// 사망 중인 경우에만 이전 이동 상황을 지우고 Idle로 복귀
    /// </summary>
    public void Revive()
    {
        stateGraph.Revive();
        PublishStateChange();
    }

    private void PublishStateChange()
    {
        PlayerState nextState = stateGraph.CurrentState;
        if (currentState == nextState)
        {
            return;
        }

        PlayerState previousState = currentState;
        currentState = nextState;
        StateChanged?.Invoke(previousState, nextState);
    }
}
