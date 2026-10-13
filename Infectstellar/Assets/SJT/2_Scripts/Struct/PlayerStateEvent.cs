/// <summary>발생 시각과 순서, 생사 버전을 포함하는 확정값 RPC 기록입니다.</summary>
public readonly struct PlayerStateEvent
{
    public readonly PlayerStateEventType Type; // 시간에 맞춰 표시할 변경의 종류
    public readonly bool Value; // 토글 대신 적용할 켜짐·생존 확정값
    public readonly double Time; // 변경이 발생한 Photon 시각(초)
    public readonly int Sequence; // 송신자가 변경마다 증가시킨 순서 번호
    public readonly int LifeVersion; // 변경이 속한 사망·부활 버전

    /// <summary>토글 대신 확정값과 제어자의 증가 순서를 보관합니다.</summary>
    public PlayerStateEvent(
        PlayerStateEventType type, // 손전등·손들기·생사 중 변경의 종류
        bool value, // 상대에도 그대로 적용할 확정값
        double time, // 송신자가 상태를 기록한 Photon 시각(초)
        int sequence, // 송신자의 마지막 상태 변경 순서
        int lifeVersion) // 이전 생사의 기록을 제외할 버전 번호
    {
        Type = type;
        Value = value;
        Time = time;
        Sequence = sequence;
        LifeVersion = lifeVersion;
    }
}
