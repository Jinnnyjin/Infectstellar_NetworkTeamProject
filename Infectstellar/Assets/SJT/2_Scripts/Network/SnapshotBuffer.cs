using System;
using System.Collections.Generic;

/// <summary>시간순 스냅샷을 보관하고 재생 시각 앞뒤의 기록을 선택합니다.</summary>
public sealed class SnapshotBuffer<T>
{
    private const int c_capacity = 32;
    private readonly List<T> snapshots = new List<T>(c_capacity);
    private readonly float delay;
    private readonly Func<T, double> d_getTime;

    /// <summary>보간 지연과 스냅샷 시각을 읽을 함수를 지정합니다.</summary>
    public SnapshotBuffer(float delay, Func<T, double> getTime)
    {
        if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }
        this.delay = delay;
        d_getTime = getTime ?? throw new ArgumentNullException(nameof(getTime));
    }

    /// <summary>마지막 기록보다 새로운 유효한 시각의 스냅샷만 추가합니다.</summary>
    public void Push(T snapshot)
    {
        double time = d_getTime(snapshot);
        if (double.IsNaN(time) || double.IsInfinity(time) ||
            (snapshots.Count > 0 && time <= d_getTime(snapshots[snapshots.Count - 1])))
        {
            return;
        }
        if (snapshots.Count == c_capacity)
        {
            snapshots.RemoveAt(0);
        }
        snapshots.Add(snapshot);
    }

    /// <summary>기록이 하나이면 같은 기록을 반환하며 외삽 여부는 호출자가 정합니다.</summary>
    public bool TrySample(double now, out T from, out T to, out double playbackTime)
    {
        from = default;
        to = default;
        playbackTime = now - delay;
        if (snapshots.Count == 0 || double.IsNaN(now) || double.IsInfinity(now))
        {
            return false;
        }
        while (snapshots.Count >= 2 && d_getTime(snapshots[1]) <= playbackTime)
        {
            snapshots.RemoveAt(0);
        }
        from = snapshots[0];
        to = snapshots.Count > 1 ? snapshots[1] : from;
        return true;
    }

    /// <summary>이전 송신자의 기록을 모두 비웁니다.</summary>
    public void Clear()
    {
        snapshots.Clear();
    }
}
