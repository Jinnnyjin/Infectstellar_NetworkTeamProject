using System;
using UnityEngine;

/// <summary>
/// 입력·물리 상황을 HFSM에 전달하고 한 번 이동한 뒤 최종 상태의 변경을 알립니다.
/// Controller가 Initialize를 호출한 뒤 Tick·Die·Revive를 사용합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class _PlayerStateMachine : MonoBehaviour
{
    private PlayerStateGraph stateGraph;
    private PlayerState currentState = PlayerState.Idle;
    private PlayerMotor motor;

    /// <summary>
    /// 마지막으로 확정한 최종 상태를 반환합니다. 초기 상태는 Idle입니다.
    /// </summary>
    public PlayerState CurrentState => currentState;

    /// <summary>
    /// 플레이어가 사망 상태가 아닌지 반환합니다.
    /// </summary>
    public bool IsAlive => currentState != PlayerState.Dead;

    /// <summary>
    /// 최종 상태가 바뀐 뒤 변경 전과 변경 후의 상태를 한 번만 알립니다.
    /// </summary>
    public event Action<PlayerState, PlayerState> StateChanged;

    /// <summary>
    /// 그래프를 종료하고 보관한 이벤트 구독 참조를 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        stateGraph?.Exit();
        stateGraph = null;
        StateChanged = null;
    }

    /// <summary>
    /// Controller가 호출하며 그래프를 준비하고 Motor와 이동 설정을 연결합니다.
    /// </summary>
    internal void Initialize(PlayerMotor motor, PlayerMovementSettings settings)
    {
        stateGraph ??= new PlayerStateGraph();

        this.motor = motor;
        stateGraph.Configure(settings);
    }

    /// <summary>
    /// 이동 전 명령 생성, 한 번의 물리 실행, 이동 후 상태 보정을 순서대로 진행합니다.
    /// </summary>
    internal void Tick(PlayerInputFrame input, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        motor.RefreshGrounded();
        PlayerMovementCommand command = stateGraph.CreateMovementCommand(input, motor.IsGrounded, motor.VerticalVelocity, motor.IsCrouching, motor.CanStand());
        motor.Simulate(command, deltaTime);
        stateGraph.UpdateStateAfterMovement(motor.IsGrounded, motor.VerticalVelocity, motor.IsCrouching, motor.CanStand());
        motor.SetCrouching(stateGraph.WantsCrouch);
        PublishStateChange();
    }

    /// <summary>
    /// 모든 생존 상태에서 즉시 사망 상태로 전환합니다.
    /// </summary>
    public void Die()
    {
        stateGraph.Die();
        PublishStateChange();
    }

    /// <summary>
    /// 사망 중인 경우에만 이전 이동 상황을 지우고 Idle로 복귀합니다.
    /// </summary>
    public void Revive()
    {
        stateGraph.Revive();
        PublishStateChange();
    }

    /// <summary>
    /// 부모·자식의 내부 전환을 완료한 뒤 최종 상태의 변경만 알립니다.
    /// </summary>
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
