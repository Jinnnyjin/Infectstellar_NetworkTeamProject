/// <summary>발생 시각과 순서, 생사 버전을 포함하는 확정값 RPC 기록입니다.</summary>
public readonly struct PlayerStateEvent
{
    public readonly PlayerStateEventType Type;
    public readonly bool Value;
    public readonly double Time;
    public readonly int Sequence;
    public readonly int LifeVersion;

    /// <summary>토글 대신 확정값과 제어자의 증가 순서를 보관합니다.</summary>
    public PlayerStateEvent(PlayerStateEventType type, bool value, double time, int sequence, int lifeVersion)
    {
        Type = type;
        Value = value;
        Time = time;
        Sequence = sequence;
        LifeVersion = lifeVersion;
    }
}
