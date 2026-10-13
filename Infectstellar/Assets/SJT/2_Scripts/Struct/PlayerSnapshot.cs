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

    /// <summary>PUN 스트림에 기본 지원 타입 10개를 약속한 순서로 기록합니다.</summary>
    public void WriteTo(PhotonStream stream)
    {
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
    public static PlayerSnapshot ReadFrom(PhotonStream stream)
    {
        double time = (double)stream.ReceiveNext();
        Vector3 position = (Vector3)stream.ReceiveNext();
        Vector3 velocity = (Vector3)stream.ReceiveNext();
        float yaw = (float)stream.ReceiveNext();
        float pitch = (float)stream.ReceiveNext();
        PlayerState state = (PlayerState)(int)stream.ReceiveNext();
        Vector2 direction = (Vector2)stream.ReceiveNext();
        bool crouching = (bool)stream.ReceiveNext();
        int rate = (int)stream.ReceiveNext();
        int lifeVersion = (int)stream.ReceiveNext();

        return new PlayerSnapshot(time, position, velocity, yaw, pitch,
            state, direction, crouching, rate, lifeVersion);
    }

    /// <summary>시각·상태·전송 등급과 이동 수치가 유효한지 확인합니다.</summary>
    public bool IsValid()
    {
        bool validTime = !double.IsNaN(Time) && !double.IsInfinity(Time);
        bool validState = State >= PlayerState.Idle && State <= PlayerState.Dead;
        bool validRate = Rate == 10 || Rate == 15 || Rate == 30;
        if (!validTime || !validState || !validRate || LifeVersion < 0)
        {
            return false;
        }

        bool validPosition = IsFinite(Position.x) && IsFinite(Position.y) && IsFinite(Position.z);
        bool validVelocity = IsFinite(Velocity.x) && IsFinite(Velocity.y) && IsFinite(Velocity.z);
        bool validAngles = IsFinite(Yaw) && IsFinite(Pitch);
        bool validDirection = IsFinite(Direction.x) && IsFinite(Direction.y);
        return validPosition && validVelocity && validAngles && validDirection;
    }

    /// <summary>이동·각도 수치에서 NaN과 무한대를 제외합니다.</summary>
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
