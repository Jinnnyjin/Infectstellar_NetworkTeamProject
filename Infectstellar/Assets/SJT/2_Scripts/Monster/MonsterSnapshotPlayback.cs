using System;
using UnityEngine;

/// <summary>몬스터의 위치·회전을 보간하고 마지막 속도로 짧게 외삽합니다.</summary>
public sealed class MonsterSnapshotPlayback
{
    private const float c_maxExtrapolation = 0.1f;
    private readonly SnapshotBuffer<MonsterSnapshot> buffer;

    /// <summary>스냅샷 도착을 기다릴 보간 지연을 지정합니다.</summary>
    public MonsterSnapshotPlayback(float bufferDelay)
    {
        buffer = new SnapshotBuffer<MonsterSnapshot>(bufferDelay, snapshot => snapshot.Time);
    }

    /// <summary>새 스냅샷을 시간순 버퍼에 추가합니다.</summary>
    public void PushSnapshot(MonsterSnapshot snapshot)
    {
        buffer.Push(snapshot);
    }

    /// <summary>재생 시각의 자세를 계산하며 행동 정보는 앞쪽 기록에서 가져옵니다.</summary>
    public MonsterSnapshot? GetPlaybackSnapshot(double now)
    {
        if (!buffer.TrySample(now, out MonsterSnapshot from, out MonsterSnapshot to, out double time))
        {
            return null;
        }
        if (from.Time == to.Time)
        {
            float duration = Mathf.Clamp((float)(time - from.Time), 0f, c_maxExtrapolation);
            return new MonsterSnapshot(from.Time + duration, from.Position + from.Velocity * duration,
                from.YRotation, from.Velocity, from.State, from.StateEndsAt);
        }
        float progress = Mathf.Clamp01((float)((time - from.Time) / (to.Time - from.Time)));
        return new MonsterSnapshot(Math.Max(from.Time, time), Vector3.Lerp(from.Position, to.Position, progress),
            Mathf.LerpAngle(from.YRotation, to.YRotation, progress), from.Velocity, from.State, from.StateEndsAt);
    }

    /// <summary>권한 변경 전의 재생 기록을 비웁니다.</summary>
    public void Clear()
    {
        buffer.Clear();
    }
}
