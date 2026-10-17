using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class _PlayerSnapShotPlayback
{
    private const int c_maxSnapShots = 32; // 스냅샷 버퍼의 최대 크기
    private const float c_maxExtrapolationTime = 0.1f;
    private readonly List<_PlayerSnapShot> snapShotBuffer = new List<_PlayerSnapShot>(c_maxSnapShots);
    private readonly float bufferDelay; // 다음 스냅샷이 도착할 여유를 주기 위해 상대 움직임을 늦춰 보여주는 시간(초).

    public _PlayerSnapShotPlayback(float _bufferDelay)
    {
        if (float.IsNaN(_bufferDelay) || float.IsInfinity(_bufferDelay) || _bufferDelay < 0f)
        {
            throw new ArgumentOutOfRangeException($"[_PlayerSnapShotPlayback] 보간 지연은 0 이상의 유한한 값이어야 합니다. _bufferDelay : {_bufferDelay}");
        }

        bufferDelay = _bufferDelay;
    }

    public void PushSnapShot(_PlayerSnapShot snapShot)
    {
        if (double.IsNaN(snapShot.Time) || double.IsInfinity(snapShot.Time))
        {
            return;
        }

        // 마지막 기록 시간보다 이후의 새로운 시각의 기록만 보관한다.
        if (snapShotBuffer.Count > 0 && snapShot.Time <= snapShotBuffer[snapShotBuffer.Count - 1].Time)
        {
            return;
        }

        // 버퍼의 최대 크기보다 커진 경우, 가장 오래된 스냅샷을 제거한다
        if (snapShotBuffer.Count >= c_maxSnapShots)
        {
            snapShotBuffer.RemoveAt(0);
        }

        snapShotBuffer.Add(snapShot);
    }

    /// <summary>버퍼 상태에 따라 보간 또는 외삽한 스냅샷을 반환합니다.</summary>
    public _PlayerSnapShot? GetPlaybackSnapShot(double currentNetworkTime)
    {
        if (snapShotBuffer.Count == 0 || double.IsNaN(currentNetworkTime) || double.IsInfinity(currentNetworkTime))
        {
            return null;
        }

        // 현재 재생할 시간, 매개변수로 받은 현재 시간에서 보간지연만큼 이전의 시간을 계산한다.
        double playbackTime = currentNetworkTime - bufferDelay;

        // 현재 재생 시각 앞뒤의 기록을 남기고, 더 오래된 기록은 제거한다.
        // snapShotBuffer[0] : 재생 시각 직전 또는 같은 시각의 스냅샷(from)
        // snapShotBuffer[1] : 재생 시각 직후의 스냅샷(to)
        while (snapShotBuffer.Count >= 2 && snapShotBuffer[1].Time <= playbackTime)
        {
            snapShotBuffer.RemoveAt(0);
        }

        _PlayerSnapShot from = snapShotBuffer[0];
        if (snapShotBuffer.Count == 1)
        {
            return ExtrapolateSnapShot(from, playbackTime);
        }

        _PlayerSnapShot to = snapShotBuffer[1];

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
        snapShotBuffer.Clear();
    }
}
