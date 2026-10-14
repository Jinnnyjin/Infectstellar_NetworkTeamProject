using Photon.Pun;
using UnityEngine;

/// <summary>같은 Photon 시간축에서 재생할 플레이어의 확정된 이동 기록입니다.</summary>
public readonly struct PlayerSnapshot
{
    public readonly double Time; // 이 상태를 기록한 Photon 시각(초)
    public readonly Vector3 Position; // 기록 당시 플레이어의 월드 위치
    public readonly Vector3 Velocity; // 실제 이동 속도(방향과 빠르기, 초당 이동량)
    public readonly float Yaw; // 몸이 좌우로 돌아간 각도(도)
    public readonly float Pitch; // 시선이 위아래로 기울어진 각도(도)
    public readonly PlayerState State; // 대기·걷기·사망 등 현재 플레이어 상태
    public readonly Vector2 Direction; // 애니메이션에 쓸 이동 입력(X=좌우, Y=앞뒤)
    public readonly bool Crouching; // 앉은 자세인지 여부
    public readonly int Rate; // 기록 당시의 초당 스냅샷 전송 횟수(Hz)
    public readonly int LifeVersion; // 사망·부활마다 증가하는 번호(이전 생사 상태의 기록을 구분)

    /// <summary>스냅샷의 시간·이동·자세·전송 빈도·생사 버전을 함께 보관합니다.</summary>
    public PlayerSnapshot(
        double time, // 송신자가 상태를 기록한 Photon 시각(초)
        Vector3 position, // 기록 당시 월드 위치(m)
        Vector3 velocity, // 충돌 처리 후 실제 이동 속도(m/s)
        float yaw, // 몸의 좌우 회전(도)
        float pitch, // 상하 시선 각도(도)
        PlayerState state, // 전송된 플레이어 상태 번호
        Vector2 direction, // 애니메이션에 사용할 좌우·앞뒤 이동 입력
        bool crouching, // 송신자가 확정한 웅크림 여부
        int rate, // 기록 당시 전송 등급(Hz)
        int lifeVersion) // 이전 생사의 기록을 제외할 버전 번호
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

    /// <summary>PUN 스트림에 기본 지원 타입 10개를 약속한 순서로 기록합니다.</summary>
    public void WriteTo(
        PhotonStream stream) // 정해진 타입 순서로 읽거나 쓸 PUN 스트림
    {
        // 송신·수신은 같은 타입과 순서를 사용해야 한다. 한 항목만 옮겨도 뒤 값들이 잘못 복원된다.
        stream.SendNext(Time);
        stream.SendNext(Position);
        stream.SendNext(Velocity);
        stream.SendNext(Yaw);
        stream.SendNext(Pitch);
        stream.SendNext((int)State);
        stream.SendNext(Direction);
        stream.SendNext(Crouching);
        stream.SendNext(Rate);
        stream.SendNext(LifeVersion);
    }

    /// <summary>쓰기와 같은 순서로 읽어 하나의 스냅샷을 복원합니다.</summary>
    public static PlayerSnapshot ReadFrom(
        PhotonStream stream) // 정해진 타입 순서로 읽거나 쓸 PUN 스트림
    {
        double time = (double)stream.ReceiveNext(); // 복원한 기록 발생 시각(초)
        Vector3 position = (Vector3)stream.ReceiveNext(); // 복원한 월드 위치(m)
        Vector3 velocity = (Vector3)stream.ReceiveNext(); // 복원한 실제 이동 속도(m/s)
        float yaw = (float)stream.ReceiveNext(); // 복원한 몸 회전(도)
        float pitch = (float)stream.ReceiveNext(); // 복원한 상하 시선(도)
        PlayerState state = (PlayerState)(int)stream.ReceiveNext(); // 정수 번호에서 복원한 플레이어 상태
        Vector2 direction = (Vector2)stream.ReceiveNext(); // 복원한 애니메이션용 이동 입력
        bool crouching = (bool)stream.ReceiveNext(); // 복원한 실제 웅크림 여부
        int rate = (int)stream.ReceiveNext(); // 복원한 전송 등급(Hz)
        int lifeVersion = (int)stream.ReceiveNext(); // 복원한 사망·부활 버전

        return new PlayerSnapshot(time, position, velocity, yaw, pitch,
            state, direction, crouching, rate, lifeVersion);
    }

    /// <summary>시각·상태·전송 등급과 이동 수치가 유효한지 확인합니다.</summary>
    public bool IsValid()
    {
        bool validTime = !double.IsNaN(Time) && !double.IsInfinity(Time); // 시각이 정상적인 유한값인지 여부
        bool validState = State >= PlayerState.Idle && State <= PlayerState.Dead; // 상태 번호가 현재 enum의 허용 범위인지 여부
        bool validRate = Rate == PlayerNetworkConstants.c_lowSendRate // 송수신 계약이 지원하는 전송 등급인지 여부
            || Rate == PlayerNetworkConstants.c_middleSendRate
            || Rate == PlayerNetworkConstants.c_highSendRate;
        if (!validTime || !validState || !validRate || LifeVersion < 0)
        {
            return false;
        }

        // NaN·무한대가 보간과 외삽 전체로 퍼지지 않도록 실제 수치의 각 성분도 검사한다.
        bool validPosition = IsFinite(Position.x) && IsFinite(Position.y) && IsFinite(Position.z); // 위치 세 축이 유한값인지 여부
        bool validVelocity = IsFinite(Velocity.x) && IsFinite(Velocity.y) && IsFinite(Velocity.z); // 속도 세 축이 유한값인지 여부
        bool validAngles = IsFinite(Yaw) && IsFinite(Pitch); // 몸·시선 각도가 유한값인지 여부
        bool validDirection = IsFinite(Direction.x) && IsFinite(Direction.y); // 이동 입력 두 축이 유한값인지 여부
        return validPosition && validVelocity && validAngles && validDirection;
    }

    /// <summary>이동·각도 수치에서 NaN과 무한대를 제외합니다.</summary>
    private static bool IsFinite(
        float value) // NaN·무한대가 아닌지 검사할 위치·속도·각도 성분
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
