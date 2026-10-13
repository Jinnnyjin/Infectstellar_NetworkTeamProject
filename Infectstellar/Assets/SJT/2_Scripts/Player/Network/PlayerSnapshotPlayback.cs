using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>원격 스냅샷과 RPC를 단조 증가하는 Photon 재생 시계에 맞춰 표시합니다.</summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class PlayerSnapshotPlayback : MonoBehaviour
{
    private const int c_snapshotCapacity = 128; // 버퍼에 보관할 스냅샷 수의 상한
    private const float c_maxExtrapolation = 0.1f; // 다음 기록이 없을 때 속도로 위치를 예측할 최대 시간(초)
    private const float c_fixedBufferDelay = 0.1f; // 적응형을 끈 상태에서 고정으로 기다릴 시간(초)
    private const float c_minBufferDelay = 0.08f; // 적응형 보간에서 기다릴 최소 시간(초)
    private const float c_maxBufferDelay = 0.3f; // 적응형 보간에서 기다릴 최대 시간(초)
    private const float c_bufferedIntervals = 2f; // 보간을 위해 기다릴 스냅샷 전송 간격 수
    private const float c_jitterMargin = 2f; // 도착 간격의 흔들림에 곱할 여유 배수
    private const float c_jitterSmoothing = 0.1f; // 새로 측정한 도착 흔들림을 기존 값에 반영할 비율
    private const float c_clockCorrection = 0.5f; // 목표 시각과의 차이를 재생 속도에 반영할 세기
    private const float c_minPlaybackSpeed = 0.95f; // 재생 속도의 최소 배율(1이면 정상 속도)
    private const float c_maxPlaybackSpeed = 1.05f; // 재생 속도의 최대 배율(1이면 정상 속도)

    private readonly List<PlayerSnapshot> snapshots = new List<PlayerSnapshot>(c_snapshotCapacity); // 발생 시각순으로 모아 둔 이동·자세 기록
    private readonly List<PlayerStateEvent> events = new List<PlayerStateEvent>(); // 재생 시각에 맞춰 적용할 손전등·손들기·생사 변경
    private readonly int[] appliedSequences = new int[3]; // 변경 종류별로 마지막에 적용한 순서 번호
    private PlayerNetworkSync networkSync; // 네트워크 준비 상태와 재생 설정을 확인할 컴포넌트
    private PlayerStateMachine stateMachine; // 원격 플레이어의 이동·생사 상태를 반영할 컴포넌트
    private PlayerMotor motor; // 원격 플레이어의 앉기 자세를 반영할 컴포넌트
    private PlayerLook playerLook; // 원격 플레이어의 위아래 시선 각도를 반영할 컴포넌트
    private FlashlightController flashlight; // 원격 플레이어의 손전등을 켜고 끌 컴포넌트
    private PlayerAnimationController animationController; // 이동 방향과 손들기 상태를 전달할 애니메이션 컴포넌트
    private Transform body; // 재생한 위치와 몸의 좌우 회전을 적용할 Transform
    private bool isRemote; // 다른 사람이 조종하는 원격 플레이어인지 여부
    private bool hasInitialState; // 전체 초기 상태를 받아 재생을 시작할 준비가 됐는지 여부
    private bool isAlive; // 현재 재생 화면에서 확정된 생존 여부
    private int appliedLifeVersion; // 현재 적용한 생사 전환의 버전 번호(과거 상태를 거르는 기준)
    private int initialSequence; // 초기 응답에 이미 포함된 마지막 변경 순서 번호
    private double initialTime; // 초기 응답의 기준 시각(오래된 기록을 거르는 기준, 초)
    private double playbackTime; // 지금 화면에 재생하고 있는 Photon 시각(초)
    private double latestSampleTime = double.NegativeInfinity; // 받은 스냅샷 중 가장 최신 기록의 발생 시각(초)
    private double lastArrivalTime; // 가장 최신 스냅샷을 이쪽에서 받은 시각(초)
    private int lastRate = 30; // 재생 지연 계산에 쓰는 상대의 초당 스냅샷 전송 횟수(Hz)
    private float jitter; // 스냅샷 발생 간격과 실제 도착 간격의 차이를 완화한 값(초)

#if UNITY_EDITOR
    [SerializeField] private float playbackDelayMs; // 현재 시각보다 화면 재생이 실제로 뒤처진 시간(ms)
    [SerializeField] private float targetDelayMs; // 부드러운 재생을 위해 목표로 잡은 지연 시간(ms)
    [SerializeField] private float playbackSpeed = 1f; // 재생 시계의 진행 배율(1이면 정상 속도)
    [SerializeField] private float extrapolationMs; // 마지막 속도로 위치를 예측하는 데 사용한 시간(ms)
    [SerializeField] private float correctionMs; // 목표 재생 시각과 현재 재생 시각의 차이(ms, 양수면 뒤처짐)
    [SerializeField] private int bufferedSnapshots; // 현재 버퍼에 남아 있는 스냅샷 수
    [SerializeField] private int pendingEvents; // 아직 적용하지 않고 기다리는 상태 변경 수
    [SerializeField] private int underrunFrames; // 다음 스냅샷이 없어 위치 예측 구간에 들어간 누적 프레임 수
    [SerializeField] private int lateEvents; // 재생 시각 기준으로 한 프레임 넘게 늦게 적용한 상태 변경의 누적 수
#endif

    /// <summary>전체 초기 상태를 확인한 뒤에만 원격 표시를 진행합니다.</summary>
    public bool HasInitialState => hasInitialState;

    /// <summary>매 프레임 재사용할 컴포넌트와 로컬·원격 역할을 연결합니다.</summary>
    public void Initialize(PlayerNetworkSync networkSync, PlayerStateMachine stateMachine, PlayerMotor motor, PlayerLook playerLook, FlashlightController flashlight, PlayerAnimationController animationController, bool isRemote)
    {
        this.networkSync = networkSync;
        this.stateMachine = stateMachine;
        this.motor = motor;
        this.playerLook = playerLook;
        this.flashlight = flashlight;
        this.animationController = animationController;
        this.isRemote = isRemote;
        body = transform;
    }

    /// <summary>초기 응답보다 오래된 기록을 버리고 전체 표시 상태와 순서 기준을 준비합니다.</summary>
    public void SetInitialState(PlayerSnapshot snapshot, bool light, bool handsUp, int sequence, double arrivalTime)
    {
        initialTime = snapshot.Time;
        initialSequence = sequence;
        playbackTime = snapshot.Time;
        appliedLifeVersion = snapshot.LifeVersion;
        isAlive = snapshot.State != PlayerState.Dead;
        for (int i = 0; i < appliedSequences.Length; i++)
        {
            appliedSequences[i] = sequence;
        }
        snapshots.RemoveAll(s => s.Time <= initialTime);
        events.RemoveAll(e => e.Time < initialTime || e.Sequence <= sequence);
        snapshots.Insert(0, snapshot);
        latestSampleTime = Math.Max(latestSampleTime, snapshot.Time);
        lastRate = snapshot.Rate;
        lastArrivalTime = arrivalTime;
        hasInitialState = true;
        stateMachine.ApplyRemoteState(snapshot.State);
        flashlight.SetFlashlightEnabled(isAlive && light);
        animationController.SetHandsUp(handsUp);
        ApplyPose(snapshot, snapshot, 0f, snapshot.Position);
    }

    /// <summary>도착 순서가 바뀌어도 시각으로 정렬하고 거리 계산에는 최신 기록만 반환합니다.</summary>
    public bool PushSnapshot(PlayerSnapshot snapshot, double arrivalTime)
    {
        if (hasInitialState && snapshot.Time <= initialTime)
        {
            return false;
        }
        int index = snapshots.Count;
        while (index > 0 && snapshots[index - 1].Time > snapshot.Time)
        {
            index--;
        }
        if ((index > 0 && snapshots[index - 1].Time == snapshot.Time) || (index < snapshots.Count && snapshots[index].Time == snapshot.Time))
        {
            return false;
        }
        snapshots.Insert(index, snapshot);
        if (snapshots.Count > c_snapshotCapacity)
        {
            snapshots.RemoveAt(0);
        }
        if (snapshot.Time <= latestSampleTime)
        {
            return false;
        }
        UpdateArrivalJitter(snapshot, arrivalTime);
        latestSampleTime = snapshot.Time;
        lastArrivalTime = arrivalTime;
        lastRate = snapshot.Rate;
        return true;
    }

    /// <summary>발생 간격과 도착 간격의 차이를 완화합니다. 전송 등급이 바뀌면 새 간격으로 시작합니다.</summary>
    private void UpdateArrivalJitter(PlayerSnapshot snapshot, double arrivalTime)
    {
        if (double.IsNegativeInfinity(latestSampleTime) || lastRate != snapshot.Rate)
        {
            jitter = 0f;
            return;
        }

        double arrivalInterval = arrivalTime - lastArrivalTime;
        double sampleInterval = snapshot.Time - latestSampleTime;
        float intervalDifference = (float)Math.Abs(arrivalInterval - sampleInterval);
        float limitedDifference = Mathf.Min(intervalDifference, c_maxBufferDelay);
        jitter = Mathf.Lerp(jitter, limitedDifference, c_jitterSmoothing);
    }

    /// <summary>RPC를 발생 시각·변경 순서로 정렬하고 이미 확인한 과거 변경은 버립니다.</summary>
    public void PushEvent(PlayerStateEvent stateEvent)
    {
        int typeIndex = (int)stateEvent.Type;
        if (hasInitialState && (stateEvent.Time < initialTime || stateEvent.Sequence <= initialSequence || stateEvent.Sequence <= appliedSequences[typeIndex]))
        {
            return;
        }
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Sequence == stateEvent.Sequence)
            {
                return;
            }
        }
        int index = events.Count;
        while (index > 0 && (events[index - 1].Time > stateEvent.Time || (events[index - 1].Time == stateEvent.Time && events[index - 1].Sequence > stateEvent.Sequence)))
        {
            index--;
        }
        events.Insert(index, stateEvent);
    }

    /// <summary>애니메이션 갱신보다 먼저 재생 시계·RPC·이동 표시를 진행합니다.</summary>
    private void Update()
    {
        if (!isRemote || !hasInitialState || networkSync == null || !networkSync.IsInitialized || !PhotonNetwork.InRoom || snapshots.Count == 0)
        {
            return;
        }
        double now = PhotonNetwork.Time;
        AdvancePlayback(now, Time.unscaledDeltaTime, networkSync.UseAdaptiveBuffer);
    }

    /// <summary>전달된 시각과 경과 시간으로 재생을 진행합니다. 위치 정지 중에도 RPC와 시계는 계속 갱신합니다.</summary>
    private void AdvancePlayback(double now, float deltaTime, bool adaptive)
    {
        float delay = CalculateBufferDelay(adaptive);
        double targetTime = now - delay;
        double timeDifference = targetTime - playbackTime;
        float speed = CalculatePlaybackSpeed(timeDifference);

        AdvanceClock(now, deltaTime, speed);
        ProcessEvents();
        DiscardPlayedSnapshots();
        ApplyBufferedPose();
#if UNITY_EDITOR
        UpdateDiagnostics(now, delay, speed, timeDifference);
#endif
    }

    /// <summary>두 번의 전송 간격에 도착 흔들림 여유를 더해 기다릴 시간을 정합니다.</summary>
    private float CalculateBufferDelay(bool adaptive)
    {
        if (!adaptive)
        {
            return c_fixedBufferDelay;
        }

        float snapshotDelay = c_bufferedIntervals / lastRate;
        float jitterDelay = jitter * c_jitterMargin;
        float desiredDelay = snapshotDelay + jitterDelay;
        return Mathf.Clamp(desiredDelay, c_minBufferDelay, c_maxBufferDelay);
    }

    /// <summary>목표 시각보다 뒤처지면 조금 빠르게, 앞서면 조금 느리게 재생합니다.</summary>
    private static float CalculatePlaybackSpeed(double timeDifference)
    {
        float speedCorrection = (float)timeDifference * c_clockCorrection;
        float desiredSpeed = 1f + speedCorrection;
        return Mathf.Clamp(desiredSpeed, c_minPlaybackSpeed, c_maxPlaybackSpeed);
    }

    /// <summary>재생 시각을 앞으로만 진행하며 현재 Photon 시각을 넘지 않게 합니다.</summary>
    private void AdvanceClock(double now, float deltaTime, float speed)
    {
        double nextTime = playbackTime + deltaTime * speed;
        double limitedTime = Math.Min(now, nextTime);
        playbackTime = Math.Max(playbackTime, limitedTime);
    }

    /// <summary>재생 시각을 둘러싼 두 기록만 남도록 이미 지난 앞쪽 기록을 제거합니다.</summary>
    private void DiscardPlayedSnapshots()
    {
        while (snapshots.Count > 1 && snapshots[1].Time <= playbackTime)
        {
            snapshots.RemoveAt(0);
        }
    }

    /// <summary>두 기록이 있으면 보간하고 하나만 남으면 마지막 속도로 짧게 예측합니다.</summary>
    private void ApplyBufferedPose()
    {
        PlayerSnapshot from = snapshots[0];
#if UNITY_EDITOR
        extrapolationMs = 0f;
#endif
        if (snapshots.Count > 1)
        {
            PlayerSnapshot to = snapshots[1];
            float progress = CalculateInterpolationProgress(from.Time, to.Time);
            Vector3 position = Vector3.Lerp(from.Position, to.Position, progress);
            ApplyPose(from, to, progress, position);
            return;
        }

        float elapsed = (float)(playbackTime - from.Time);
        float predictionTime = Mathf.Clamp(elapsed, 0f, c_maxExtrapolation);
        Vector3 predictedPosition = from.Position + from.Velocity * predictionTime;
        ApplyPose(from, from, 0f, predictedPosition);
#if UNITY_EDITOR
        extrapolationMs = predictionTime * 1000f;
        if (playbackTime > from.Time)
        {
            underrunFrames++;
        }
#endif
    }

    /// <summary>기록 사이에서 얼마나 진행했는지 0~1로 반환합니다.</summary>
    private float CalculateInterpolationProgress(double fromTime, double toTime)
    {
        double interval = toTime - fromTime;
        if (interval <= 0d)
        {
            return 0f;
        }

        // Photon 절대 시각은 double로 빼고, 최종 비율만 float로 바꿔 정밀도를 유지한다.
        double elapsed = playbackTime - fromTime;
        float progress = (float)(elapsed / interval);
        return Mathf.Clamp01(progress);
    }

#if UNITY_EDITOR
    /// <summary>동작 계산과 분리하여 현재 재생 상태를 Inspector에 표시합니다.</summary>
    private void UpdateDiagnostics(double now, float delay, float speed, double timeDifference)
    {
        playbackDelayMs = (float)(now - playbackTime) * 1000f;
        targetDelayMs = delay * 1000f;
        playbackSpeed = speed;
        correctionMs = (float)timeDifference * 1000f;
        bufferedSnapshots = snapshots.Count;
        pendingEvents = events.Count;
    }
#endif

    /// <summary>생사 버전이 확정되지 않은 변경은 보류하고 늦은 RPC도 시간 역행 없이 한 번 적용합니다.</summary>
    private void ProcessEvents()
    {
        ProcessDueEvents();
        // 첫 순회에서 생사가 확정되면, 보류했던 같은 시각의 상태도 이번 프레임에 적용한다.
        ProcessDueEvents();
    }

    /// <summary>재생 시각이 된 변경 중 생사 버전이 확정된 것만 대기열에서 꺼냅니다.</summary>
    private void ProcessDueEvents()
    {
        for (int i = 0; i < events.Count;)
        {
            PlayerStateEvent stateEvent = events[i];
            if (stateEvent.Time > playbackTime)
            {
                break;
            }

            bool waitingForLifeState = stateEvent.Type != PlayerStateEventType.LifeState
                && stateEvent.LifeVersion > appliedLifeVersion;
            if (waitingForLifeState)
            {
                i++;
                continue;
            }

            events.RemoveAt(i);
            ApplyEvent(stateEvent);
        }
    }

    /// <summary>과거·중복 변경을 거른 뒤 종류별 확정값을 한 번 적용합니다.</summary>
    private void ApplyEvent(PlayerStateEvent stateEvent)
    {
        int typeIndex = (int)stateEvent.Type;
        bool alreadyApplied = stateEvent.Sequence <= appliedSequences[typeIndex];
        bool oldLifeVersion = stateEvent.LifeVersion < appliedLifeVersion;
        if (alreadyApplied || oldLifeVersion)
        {
            return;
        }

        appliedSequences[typeIndex] = stateEvent.Sequence;
#if UNITY_EDITOR
        if (stateEvent.Time < playbackTime - Time.unscaledDeltaTime)
        {
            lateEvents++;
        }
#endif
        switch (stateEvent.Type)
        {
            case PlayerStateEventType.Flashlight:
                flashlight.SetFlashlightEnabled(isAlive && stateEvent.Value);
                break;
            case PlayerStateEventType.HandsUp:
                animationController.SetHandsUp(stateEvent.Value);
                break;
            case PlayerStateEventType.LifeState:
                ApplyLifeState(stateEvent);
                break;
        }
    }

    /// <summary>새 생사 버전으로 전환하고 사망·부활 때 손전등을 끕니다.</summary>
    private void ApplyLifeState(PlayerStateEvent stateEvent)
    {
        if (stateEvent.LifeVersion <= appliedLifeVersion)
        {
            return;
        }

        appliedLifeVersion = stateEvent.LifeVersion;
        isAlive = stateEvent.Value;
        PlayerState state = isAlive ? PlayerState.Idle : PlayerState.Dead;
        stateMachine.ApplyRemoteState(state);
        flashlight.SetFlashlightEnabled(false);
    }

    /// <summary>움직임은 보간하고 이산 자세는 같은 생사 버전의 이전 스냅샷 시점에 적용합니다.</summary>
    private void ApplyPose(PlayerSnapshot from, PlayerSnapshot to, float progress, Vector3 position)
    {
        float yaw = Mathf.LerpAngle(from.Yaw, to.Yaw, progress);
        float pitch = Mathf.LerpAngle(from.Pitch, to.Pitch, progress);
        body.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        playerLook.ApplyRemotePitch(pitch);

        bool matchingLifeVersion = from.LifeVersion == appliedLifeVersion;
        bool matchingLifeState = (from.State != PlayerState.Dead) == isAlive;
        if (!matchingLifeVersion || !matchingLifeState)
        {
            return;
        }
        motor.ApplyRemoteCrouch(from.Crouching);
        stateMachine.ApplyRemoteState(from.State);
        animationController.SetMoveDirection(from.Direction);
    }
}
