using System;
using UnityEngine;

public class _PlayerSnapShotPlayback
{
    private const float c_maxExtrapolationTime = 0.1f;
    private readonly SnapshotBuffer<_PlayerSnapShot> buffer;

    public _PlayerSnapShotPlayback(float _bufferDelay)
    {
        buffer = new SnapshotBuffer<_PlayerSnapShot>(_bufferDelay, snapshot => snapshot.Time);
    }

    public void PushSnapShot(_PlayerSnapShot snapShot)
    {
        buffer.Push(snapShot);
    }

    /// <summary>버퍼 상태에 따라 보간 또는 외삽한 스냅샷을 반환합니다.</summary>
    public _PlayerSnapShot? GetPlaybackSnapShot(double currentNetworkTime)
    {
        if (!buffer.TrySample(currentNetworkTime, out _PlayerSnapShot from, out _PlayerSnapShot to, out double playbackTime))
        {
            return null;
        }

        if (from.Time == to.Time)
        {
            return ExtrapolateSnapShot(from, playbackTime);
        }

        return InterpolateSnapShot(from, to, playbackTime);
    }

    /// <summary>두 기록 사이의 위치·회전을 보간합니다.</summary>
    private _PlayerSnapShot InterpolateSnapShot(_PlayerSnapShot from, _PlayerSnapShot to, double playbackTime)
    {
        double interval = to.Time - from.Time; // 두 스냅샷이 기록된 시각 사이의 간격(초).
        // 두 스냅샷 사이에서 재생이 진행된 비율(0은 from, 1은 to).
        // progress : 시작 기록부터 현재 재생할 시간 시각까지 지난 시간(playbackTime - from.Time)을 두 기록 사이의 전체 시간(interval)으로 나눠, 얼마나 진행했는지 계산한 비율.
        float progress = Mathf.Clamp01((float)((playbackTime - from.Time) / interval));

        // 위치와 회전을 진행도에 맞춰 보간값을 설정
        Vector3 position = Vector3.Lerp(from.Position, to.Position, progress);
        float yRotation = Mathf.LerpAngle(from.YRotation, to.YRotation, progress);
        float viewPitch = Mathf.LerpAngle(from.ViewPitch, to.ViewPitch, progress);

        double evaluatedTime = Math.Max(from.Time, playbackTime);

        // 상태·방향·웅크림은 앞쪽 기록의 값을 유지한다.
        return new _PlayerSnapShot(
            evaluatedTime,
            position, yRotation, viewPitch,
            from.State, from.MoveDirection, from.IsCrouching,
            from.Velocity);
    }

    /// <summary>마지막 수신 속도로 위치를 최대 0.1초 예측합니다.</summary>
    private _PlayerSnapShot ExtrapolateSnapShot(_PlayerSnapShot latest, double playbackTime)
    {
        // 마지막 기록 이후 지난 시간을 0~0.1초로 제한한다.
        float extrapolationTime = Mathf.Clamp((float)(playbackTime - latest.Time), 0f, c_maxExtrapolationTime);

        // 마지막 수신 위치에 속도 × 예측 시간을 더한다.
        Vector3 position = latest.Position + latest.Velocity * extrapolationTime;
        double evaluatedTime = latest.Time + extrapolationTime;

        // 위치만 예측하고 나머지는 마지막 수신 값을 유지한다.
        return new _PlayerSnapShot(
            evaluatedTime,
            position, latest.YRotation, latest.ViewPitch,
            latest.State, latest.MoveDirection, latest.IsCrouching,
            latest.Velocity);
    }

    /// <summary>이전 재생 기록을 모두 비웁니다.</summary>
    public void Clear()
    {
        buffer.Clear();
    }
}
