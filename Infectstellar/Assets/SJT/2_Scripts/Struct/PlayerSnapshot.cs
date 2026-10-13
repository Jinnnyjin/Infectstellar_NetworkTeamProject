using UnityEngine;

/// <summary>같은 Photon 시간축에서 재생할 플레이어의 확정된 이동 기록입니다.</summary>
public readonly struct PlayerSnapshot
{
    public readonly double Time;
    public readonly Vector3 Position;
    public readonly Vector3 Velocity;
    public readonly float Yaw;
    public readonly float Pitch;
    public readonly PlayerState State;
    public readonly Vector2 Direction;
    public readonly bool Crouching;
    public readonly int Rate;
    public readonly int LifeVersion;

    /// <summary>스냅샷의 시간·이동·자세·전송 빈도·생사 버전을 함께 보관합니다.</summary>
    public PlayerSnapshot(double time, Vector3 position, Vector3 velocity, float yaw, float pitch, PlayerState state, Vector2 direction, bool crouching, int rate, int lifeVersion)
    {
        Time = time;
        Position = position;
        Velocity = velocity;
        Yaw = yaw;
        Pitch = pitch;
        State = state;
        Direction = direction;
        Crouching = crouching;
        Rate = rate;
        LifeVersion = lifeVersion;
    }
}
