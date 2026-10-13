using UnityEngine;

/// <summary>화면에 적용할 위치·회전과 이산 상태의 기준 기록을 함께 전달합니다.</summary>
internal readonly struct PlayerSnapshotPose
{
    public readonly Vector3 position; // 보간 또는 외삽으로 계산한 월드 위치(m)
    public readonly float yaw; // 몸의 좌우 회전 각도(도)
    public readonly float pitch; // 상하 시선 각도(도)
    public readonly PlayerSnapshot source; // 상태·웅크림·이동 방향을 적용할 앞쪽 기록
    public readonly float extrapolationTime; // 실제 위치 예측에 사용한 시간(초; 보간이면 0)

    /// <summary>연속 자세와 원본 기록을 하나의 읽기 전용 결과로 보관합니다.</summary>
    public PlayerSnapshotPose(
        Vector3 position, // 계산된 월드 위치(m)
        float yaw, // 계산된 몸 회전(도)
        float pitch, // 계산된 시선 회전(도)
        PlayerSnapshot source, // 이산 상태를 읽을 원본 기록
        float extrapolationTime) // 적용한 예측 시간(초)
    {
        this.position = position;
        this.yaw = yaw;
        this.pitch = pitch;
        this.source = source;
        this.extrapolationTime = extrapolationTime;
    }
}

/// <summary>Unity 객체를 변경하지 않고 기록에서 보간·외삽 자세만 계산합니다.</summary>
internal static class PlayerSnapshotPoseCalculator
{
    /// <summary>두 기록 사이의 위치와 각도를 재생 시각에 맞춰 보간합니다.</summary>
    public static PlayerSnapshotPose Interpolate(
        PlayerSnapshot from, // 앞쪽 이동 기록
        PlayerSnapshot to, // 뒤쪽 이동 기록
        double playbackTime) // 표시할 Photon 시각(초)
    {
        double interval = to.Time - from.Time; // 두 기록 사이의 시간(초)
        double elapsed = playbackTime - from.Time; // 앞 기록부터 재생 시각까지의 시간(초)
        // 진행 비율 = 경과 시간 / 기록 간격. 절대 시각은 double로 빼서 정밀도를 지킨다.
        float progress = interval > 0d ? Mathf.Clamp01((float)(elapsed / interval)) : 0f; // 두 기록 사이 진행 비율(0~1)
        Vector3 position = Vector3.Lerp(from.Position, to.Position, progress); // 위치를 직선으로 연결한 결과(m)
        float yaw = Mathf.LerpAngle(from.Yaw, to.Yaw, progress); // 350→10도 같은 경계를 짧은 방향으로 연결한 몸 회전
        float pitch = Mathf.LerpAngle(from.Pitch, to.Pitch, progress); // 같은 진행 비율로 연결한 상하 시선
        // enum·bool·입력 방향은 섞지 않고 앞쪽 기록을 적용할 때 함께 사용한다.
        return new PlayerSnapshotPose(position, yaw, pitch, from, 0f);
    }

    /// <summary>다음 기록이 없으면 마지막 실제 속도로 제한된 시간만 위치를 예측합니다.</summary>
    public static PlayerSnapshotPose Extrapolate(
        PlayerSnapshot source, // 마지막으로 확보한 이동 기록
        double playbackTime, // 표시할 Photon 시각(초)
        float maxExtrapolation) // 허용한 위치 예측 시간의 상한(초)
    {
        float elapsed = (float)(playbackTime - source.Time); // 마지막 기록 이후 재생 경과 시간(초)
        float predictionTime = Mathf.Clamp(elapsed, 0f, maxExtrapolation); // 데이터가 끊겨도 상한까지만 이동할 시간(초)
        // 위치 = 마지막 위치 + 실제 속도(m/s) × 예측 시간(s). 중력·충돌은 재계산하지 않는다.
        Vector3 position = source.Position + source.Velocity * predictionTime; // 마지막 속도를 유지한다고 가정한 위치(m)
        return new PlayerSnapshotPose(position, source.Yaw, source.Pitch, source, predictionTime);
    }
}
