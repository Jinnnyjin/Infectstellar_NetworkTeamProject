using System;
using UnityEngine;

/// <summary>
/// 외부에서 받은 상황을 HFSM에 전달하고 최종 상태의 변경을 알립니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStateMachine : MonoBehaviour
{
    private PlayerStates states;
    private PlayerState currentState = PlayerState.Idle;

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
    /// 자신의 상태 그래프를 초기화합니다.
    /// </summary>
    private void Awake()
    {
        InitializeIfNeeded();
    }

    /// <summary>
    /// 그래프를 종료하고 보관한 이벤트 구독 참조를 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        states?.Exit();
        states = null;
        StateChanged = null;
    }

    /// <summary>
    /// 외부 입력·이동 시스템이 판단한 상황을 반영합니다. 사망 중에는 무시합니다.
    /// </summary>
    /// <param name="isMoving">이동 중인지 나타냅니다.</param>
    /// <param name="wantsToRun">달리기 의도입니다. 서서 이동 중일 때 반영합니다.</param>
    /// <param name="wantsToCrouch">웅크리기 의도입니다. 공중에서는 착지할 때 반영합니다.</param>
    /// <param name="isGrounded">접지 여부입니다. 물리 판정은 호출자가 담당합니다.</param>
    /// <param name="verticalVelocity">위쪽이 양수인 수직 속도입니다. 비접지 상태에서 양수는 Jump, 0 이하는 Fall로 판단합니다.</param>
    public void UpdateMovementContext(bool isMoving, bool wantsToRun, bool wantsToCrouch, bool isGrounded, float verticalVelocity)
    {
        InitializeIfNeeded();
        states.UpdateMovementContext(isMoving, wantsToRun, wantsToCrouch, isGrounded, verticalVelocity);
        PublishStateChange();
    }

    /// <summary>
    /// 모든 생존 상태에서 즉시 사망 상태로 전환합니다.
    /// </summary>
    public void Die()
    {
        InitializeIfNeeded();
        states.Die();
        PublishStateChange();
    }

    /// <summary>
    /// 사망 중인 경우에만 이전 이동 상황을 지우고 Idle로 복귀합니다.
    /// </summary>
    public void Revive()
    {
        InitializeIfNeeded();
        states.Revive();
        PublishStateChange();
    }

    /// <summary>
    /// Awake 전의 외부 호출에도 대응하며 그래프를 중복 생성하지 않습니다.
    /// </summary>
    private void InitializeIfNeeded()
    {
        if (states != null)
        {
            return;
        }

        states = new PlayerStates();
    }

    /// <summary>
    /// 부모·자식의 내부 전환을 완료한 뒤 최종 상태의 변경만 알립니다.
    /// </summary>
    private void PublishStateChange()
    {
        PlayerState nextState = states.CurrentState;
        if (currentState == nextState)
        {
            return;
        }

        PlayerState previousState = currentState;
        currentState = nextState;
        StateChanged?.Invoke(previousState, nextState);
    }
}
