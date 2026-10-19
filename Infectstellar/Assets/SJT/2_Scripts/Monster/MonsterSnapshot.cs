using UnityEngine;

/// <summary>몬스터의 자세·행동과 네트워크 시각 기준 행동 종료 시각입니다.</summary>
public readonly struct MonsterSnapshot
{
    public readonly double Time;
    public readonly Vector3 Position;
    public readonly float YRotation;
    public readonly Vector3 Velocity;
    public readonly MonsterState State;
    public readonly double StateEndsAt;

    /// <summary>체력과 이동 목적지를 제외한 재생·권한 승계 정보를 저장합니다.</summary>
    public MonsterSnapshot(double time, Vector3 position, float yRotation, Vector3 velocity, MonsterState state, double stateEndsAt)
    {
        Time = time;
        Position = position;
        YRotation = yRotation;
        Velocity = velocity;
        State = state;
        StateEndsAt = stateEndsAt;
    }
}
