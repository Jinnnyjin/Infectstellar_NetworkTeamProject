using UnityEngine;

/// <summary>상태와 아이템 동작을 같은 재생 시계에 전달하는 확정값 RPC 기록입니다.</summary>
public readonly struct PlayerStateEvent
{
    public readonly PlayerStateEventType Type; // 시간에 맞춰 표시할 변경의 종류
    public readonly bool Value; // 토글 대신 적용할 켜짐·생존 확정값
    public readonly double Time; // 변경이 발생한 Photon 시각(초)
    public readonly int Sequence; // 송신자가 변경마다 증가시킨 순서 번호
    public readonly int LifeVersion; // 변경이 속한 사망·부활 버전
    public readonly int ItemViewId; // 동작 대상 아이템의 네트워크 식별자
    public readonly Vector3 Position; // 내려놓기·발사의 확정된 월드 시작 위치
    public readonly Quaternion Rotation; // 내려놓기·발사의 확정된 월드 시작 회전
    public readonly Vector3 Force; // 발사에 사용할 확정된 힘
    public readonly int ReleaseSequence; // 정지 보정이 속한 Drop/Shoot 순서

    /// <summary>생사 표시와 별도로 확정된 아이템 행위를 구분합니다.</summary>
    public bool IsItemAction => Type == PlayerStateEventType.Grab || Type == PlayerStateEventType.Drop
        || Type == PlayerStateEventType.Shoot || Type == PlayerStateEventType.ItemStop;

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
        ItemViewId = 0;
        Position = Vector3.zero;
        Rotation = Quaternion.identity;
        Force = Vector3.zero;
        ReleaseSequence = 0;
    }

    /// <summary>지정 아이템의 동작과 월드 자세를 재송신 없이 재생할 값으로 보관합니다.</summary>
    public PlayerStateEvent(PlayerStateEventType type, int itemViewId, Vector3 position, Quaternion rotation,
        Vector3 force, double time, int sequence, int lifeVersion, int releaseSequence = 0)
    {
        Type = type;
        Value = false;
        Time = time;
        Sequence = sequence;
        LifeVersion = lifeVersion;
        ItemViewId = itemViewId;
        Position = position;
        Rotation = rotation;
        Force = force;
        ReleaseSequence = releaseSequence;
    }

    /// <summary>아이템 RPC의 식별자와 유한한 위치·회전·힘을 검사합니다.</summary>
    public bool IsValidItemData()
    {
        return IsItemAction && ItemViewId > 0 && IsFinite(Position.x) && IsFinite(Position.y) && IsFinite(Position.z)
            && IsFinite(Rotation.x) && IsFinite(Rotation.y) && IsFinite(Rotation.z) && IsFinite(Rotation.w)
            && Rotation.x * Rotation.x + Rotation.y * Rotation.y + Rotation.z * Rotation.z + Rotation.w * Rotation.w > 0.0001f
            && IsFinite(Force.x) && IsFinite(Force.y) && IsFinite(Force.z)
            && (Type != PlayerStateEventType.ItemStop || ReleaseSequence > 0);
    }

    /// <summary>네트워크에서 사용할 수 없는 NaN과 무한대를 제외합니다.</summary>
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
