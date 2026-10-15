using UnityEngine;

/// <summary>같은 네트워크 시각에 기록한 플레이어의 위치·회전·이동 상태입니다.</summary>
public readonly struct _PlayerSnapShot
{
    public readonly double Time; // 이 상태를 기록한 네트워크 시각(초).

    public readonly Vector3 Position; // 플레이어의 월드 위치.
    public readonly float YRotation; // 몸의 좌우 회전 각도(도).
    public readonly float ViewPitch; // 시선의 상하 회전 각도(도).

    public readonly PlayerState State; // 걷기·달리기 등 확정된 이동 상태.
    public readonly Vector2 MoveDirection; // 이동 애니메이션 방향(X: 좌우, Y: 앞뒤).
    public readonly bool IsCrouching; // 실제 캡슐이 웅크린 자세인지 여부.

    /// <summary>한 시점의 위치·회전과 이동 상태를 함께 저장합니다.</summary>
    public _PlayerSnapShot(double time,
    Vector3 position, float yRotation, float viewPitch,
    PlayerState state, Vector2 moveDirection, bool isCrouching)
    {
        Time = time;

        Position = position;
        YRotation = yRotation;
        ViewPitch = viewPitch;

        State = state;
        MoveDirection = moveDirection;
        IsCrouching = isCrouching;
    }
}