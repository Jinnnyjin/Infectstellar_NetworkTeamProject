#if false // 새 시스템 전환 검증 후 삭제할 이전 구현
using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>원격 스냅샷과 RPC를 단조 증가하는 Photon 재생 시계에 맞춰 표시합니다.</summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class PlayerSnapshotPlayback : MonoBehaviour
{
    private const int c_minSnapshotCapacity = 2; // 두 기록 사이의 보간을 유지할 최소 버퍼 크기
    private const float c_normalPlaybackSpeed = 1f; // 실제 시간과 같은 속도의 재생 배율
    private const float c_minPositivePlaybackSpeed = 0.01f; // 시계가 멈추지 않도록 제한할 양수 배율 하한
    private const double c_itemResolveTimeout = 2d; // 생성 이벤트와 아이템 RPC의 연결을 기다릴 최대 시간(초)

    [Header("스냅샷 버퍼·외삽")]
    [SerializeField, Min(c_minSnapshotCapacity), Tooltip("버퍼에 보관할 스냅샷 수의 상한. 목표 지연(초) × 전송 빈도 + 1개 이상을 기준으로 도착 지연까지 고려해 여유 있게 설정")]
    private int snapshotCapacity = 128; // 버퍼에 보관할 이동 기록의 상한(개)
    [SerializeField, Min(0f), Tooltip("다음 기록이 없을 때 마지막 속도로 위치를 예측할 최대 시간(초)")]
    private float maxExtrapolation = 0.1f; // 마지막 속도로 위치를 예측할 최대 시간(초)
    [Header("재생 지연")]
    [SerializeField, Min(0f), Tooltip("적응형 버퍼를 끈 상태의 목표 재생 지연(초)")]
    private float fixedBufferDelay = 0.1f; // 고정 모드에서 기다릴 목표 지연(초)
    [SerializeField, Min(0f), Tooltip("적응형 버퍼의 최소 목표 재생 지연(초)")]
    private float minBufferDelay = 0.08f; // 적응형 모드의 최소 목표 지연(초)
    [SerializeField, Min(0f), Tooltip("적응형 버퍼의 최대 목표 재생 지연과 도착 흔들림 측정 상한(초)")]
    private float maxBufferDelay = 0.3f; // 적응형 지연과 지터 측정의 상한(초)
    [SerializeField, Min(1f), Tooltip("보간을 위해 기다릴 스냅샷 전송 간격 수")]
    private float bufferedIntervals = 2f; // 보간을 위해 대기할 전송 간격 수
    [Header("도착 지터")]
    [SerializeField, Min(0f), Tooltip("도착 간격의 흔들림에 곱할 여유 배수")]
    private float jitterMargin = 2f; // 지터에 추가 여유를 주는 배수
    [SerializeField, Range(0f, 1f), Tooltip("새 도착 흔들림 측정값을 반영할 비율")]
    private float jitterSmoothing = 0.1f; // 새 지터 측정값을 반영할 비율(0~1)
    [Header("재생 시계")]
    [SerializeField, Min(0f), Tooltip("목표 시각과의 차이를 재생 속도에 반영할 세기")]
    private float clockCorrection = 0.5f; // 시각 차이가 재생 배율에 미칠 강도
    [SerializeField, Range(c_minPositivePlaybackSpeed, c_normalPlaybackSpeed), Tooltip("재생 속도의 최소 배율(1이면 정상 속도)")]
    private float minPlaybackSpeed = 0.95f; // 정상 속도보다 느리게 재생할 때의 하한(배율)
    [SerializeField, Min(c_normalPlaybackSpeed), Tooltip("재생 속도의 최대 배율(1이면 정상 속도)")]
    private float maxPlaybackSpeed = 1.05f; // 뒤처진 시계를 빠르게 따라잡을 때의 상한(배율)

    // 버퍼·시계·이벤트 상태를 이 컴포넌트에서 함께 관리한다. 아래 기능별 메서드에서 사용한다.
    private readonly List<PlayerSnapshot> snapshots = new List<PlayerSnapshot>(); // 시각순으로 정렬된 이동 기록
    private readonly List<PlayerStateEvent> events = new List<PlayerStateEvent>(); // 아직 표시하지 않은 상태 변경
    private readonly int[] appliedSequences = new int[Enum.GetValues(typeof(PlayerStateEventType)).Length]; // 종류별 마지막 적용 순서
    private bool hasInitialState; // 초기 전체 상태의 기준이 확정됐는지 여부
    private double initialTime; // 초기 응답의 기록 시각; 과거 기록과 이벤트를 거를 기준(초)
    private int initialSequence; // 초기 응답에 이미 포함된 마지막 변경 순서
    private bool isAlive; // 현재 재생 화면에서 확정된 생존 여부
    private int appliedLifeVersion; // 현재 적용된 사망·부활 버전
    private double playbackTime; // 지금 화면에서 재생하는 Photon 시각(초)
    private double latestSampleTime = double.NegativeInfinity; // 가장 최신 기록의 발생 시각(초)
    private double lastArrivalTime; // 가장 최신 기록을 받은 시각(초)
    private int lastRate = PlayerNetworkConstants.c_highSendRate; // 상대의 마지막 전송 등급(Hz)
    private float jitter; // 발생 간격과 도착 간격의 차이를 완화한 값(초)
    private PlayerNetworkSync networkSync; // 네트워크 준비 여부와 버퍼 옵션을 제공하는 연결점
    private PlayerStateMachine stateMachine; // 원격 이동·생사 상태를 표시할 컴포넌트
    private PlayerMotor motor; // 원격 웅크림 자세를 적용할 컴포넌트
    private PlayerLook playerLook; // 원격 상하 시선을 적용할 컴포넌트
    private FlashlightController flashlight; // 원격 손전등의 확정값을 적용할 컴포넌트
    private PlayerGrabber playerGrabber; // 아이템을 잡고 던지는 컴포넌트
    private PlayerAnimationController animationController; // 방향·손들기 애니메이션을 표시할 컴포넌트
    private Transform body; // 계산된 월드 위치와 몸 회전을 적용할 대상
    private bool isRemote; // 다른 사람이 조종하는 복제본인지 여부
    private int appliedItemSequence; // 아이템 동작 사이의 과거·중복 실행을 제외할 마지막 순서
    private int initialHeldItemViewId; // 아직 생성되지 않은 초기 소지 아이템의 식별자
    private double initialItemDeadline; // 초기 아이템 연결의 대기 종료 시각
    private int unresolvedItemSequence; // 아직 대상 객체를 찾지 못한 동작 순서
    private double unresolvedItemDeadline; // 현재 미해결 동작의 대기 종료 시각

#if UNITY_EDITOR
    private float targetDelay; // 이번 프레임 목표 재생 지연(초)
    private float calculatedPlaybackSpeed = c_normalPlaybackSpeed; // 이번 프레임에 계산한 재생 배율
    private double clockTimeDifference; // 시계 진행 전 목표 시각과의 차이(초)
    private int lateEventCount; // 한 프레임 넘게 늦게 채택한 이벤트의 누적 개수
    private const float c_millisecondsPerSecond = 1000f; // 초를 Inspector의 밀리초로 바꿀 배수
    [Header("재생 진단값")]
    [SerializeField] private float playbackDelayMs; // 현재 Photon 시각에서 실제 재생 시각까지의 지연(ms)
    [SerializeField] private float targetDelayMs; // 버퍼 확보를 위해 목표로 삼은 지연(ms)
    [SerializeField] private float playbackSpeed = c_normalPlaybackSpeed; // 이번 프레임 재생 배율
    [SerializeField] private float extrapolationMs; // 마지막 속도로 위치를 예측한 시간(ms)
    [SerializeField] private float correctionMs; // 시계 진행 전 목표 시각과의 차이(ms)
    [SerializeField] private int bufferedSnapshots; // 현재 보관된 기록 개수
    [SerializeField] private int pendingEvents; // 아직 표시하지 않은 상태 변경 개수
    [SerializeField] private int underrunFrames; // 다음 기록이 없어 외삽을 사용한 누적 프레임 수
    [SerializeField] private int lateEvents; // 한 프레임 넘게 늦게 적용한 누적 상태 변경 수
#endif

    /// <summary>초기 전체 상태를 받아 원격 재생을 시작할 수 있는지 반환합니다.</summary>
    public bool HasInitialState => hasInitialState;

    /// <summary>설정 범위를 보정하고 최소 지연이 최대 지연을 넘지 않게 합니다.</summary>
    private void OnValidate()
    {
        snapshotCapacity = Mathf.Max(c_minSnapshotCapacity, snapshotCapacity);
        maxExtrapolation = Mathf.Max(0f, maxExtrapolation);
        fixedBufferDelay = Mathf.Max(0f, fixedBufferDelay);
        minBufferDelay = Mathf.Max(0f, minBufferDelay);
        maxBufferDelay = Mathf.Max(minBufferDelay, maxBufferDelay);
        bufferedIntervals = Mathf.Max(1f, bufferedIntervals);
        jitterMargin = Mathf.Max(0f, jitterMargin);
        jitterSmoothing = Mathf.Clamp01(jitterSmoothing);
        clockCorrection = Mathf.Max(0f, clockCorrection);
        minPlaybackSpeed = Mathf.Clamp(minPlaybackSpeed, c_minPositivePlaybackSpeed, c_normalPlaybackSpeed);
        maxPlaybackSpeed = Mathf.Max(c_normalPlaybackSpeed, maxPlaybackSpeed);
    }

    /// <summary>Unity 표시 컴포넌트와 역할을 연결하고 버퍼 공간을 준비합니다.</summary>
    public void Initialize(
        PlayerNetworkSync networkSync, // 생성·송수신을 담당하는 연결점
        PlayerStateMachine stateMachine, // 원격 상태 표시 대상
        PlayerMotor motor, // 원격 캡슐 자세 표시 대상
        PlayerLook playerLook, // 원격 시선 표시 대상
        FlashlightController flashlight, // 원격 손전등 표시 대상
        PlayerGrabber playerGrabber, // 원격 Grabber
        PlayerAnimationController animationController, // 원격 애니메이션 표시 대상
        bool isRemote) // 로컬 입력 대신 원격 재생을 수행할지 여부
    {
        this.networkSync = networkSync;
        this.stateMachine = stateMachine;
        this.motor = motor;
        this.playerLook = playerLook;
        this.flashlight = flashlight;
        this.playerGrabber = playerGrabber;
        this.animationController = animationController;
        this.isRemote = isRemote;
        body = transform;
        if (snapshots.Capacity < snapshotCapacity)
        {
            snapshots.Capacity = snapshotCapacity;
        }
    }

    /// <summary>초기 기록·이벤트 기준·재생 시계를 준비하고 전체 표시 상태를 적용합니다.</summary>
    public void SetInitialState(
        PlayerSnapshot snapshot, // 최초 전체 응답에 포함된 이동 기록
        bool light, // 응답 당시 손전등 확정값
        bool handsUp, // 응답 당시 손들기 확정값
        int heldItemViewId, // 응답 당시 소지 아이템; 0이면 없음
        int sequence, // 응답에 이미 포함된 마지막 상태 변경 순서
        double arrivalTime) // 응답을 받은 Photon 시각(초)
    {
        InitializeSnapshotBuffer(snapshot);
        InitializeStateEvents(snapshot, sequence);
        InitializePlaybackClock(snapshot, arrivalTime);
        hasInitialState = true;
        initialHeldItemViewId = heldItemViewId;
        initialItemDeadline = Time.realtimeSinceStartupAsDouble + c_itemResolveTimeout;
        RestoreInitialHeldItem();
        // 기준을 먼저 준비한 뒤 객체에 적용한다. 표시 중 발생하는 이벤트는 로컬 송신으로 이어지지 않는다.
        stateMachine.SetState(snapshot.State);
        flashlight.SetFlashlightEnabled(isAlive && light);
        animationController.SetHandsUp(handsUp);
        ApplyPose(PlayerSnapshotPoseCalculator.Interpolate(snapshot, snapshot, snapshot.Time));
    }

    /// <summary>기록을 보관하고 최신 기록에 대해서만 지터 기준을 갱신합니다.</summary>
    public bool PushSnapshot(
        PlayerSnapshot snapshot, // 송신자가 기록한 위치·자세
        double arrivalTime) // 이쪽에서 기록을 받은 Photon 시각(초)
    {
        bool isLatest = InsertSnapshot(snapshot); // 거리와 지터 기준을 갱신할 최신 기록인지 여부
        if (isLatest)
        {
            RecordLatestSnapshot(snapshot, arrivalTime);
        }
        return isLatest;
    }

    /// <summary>수신한 확정값 RPC를 시간순 상태 대기열에 전달합니다.</summary>
    public void PushEvent(PlayerStateEvent stateEvent) // 수신한 상태 변경 기록
    {
        InsertStateEvent(stateEvent);
    }

    /// <summary>초기 상태와 네트워크가 준비된 원격 객체의 재생을 진행합니다.</summary>
    private void Update()
    {
        if (!isRemote || !HasInitialState || networkSync == null || !networkSync.IsInitialized || !PhotonNetwork.InRoom || snapshots.Count == 0)
        {
            return;
        }
        double now = PhotonNetwork.Time; // 이번 프레임의 공통 네트워크 시각(초)
        AdvancePlayback(now, Time.unscaledDeltaTime, networkSync.UseAdaptiveBuffer);
    }

    /// <summary>시계 → 이벤트 → 기록 선택 → 자세 계산 → 표시 순서로 한 프레임을 재생합니다.</summary>
    private void AdvancePlayback(
        double now, // 현재 Photon 시각(초)
        float deltaTime, // 배속 영향을 받지 않는 프레임 시간(초)
        bool adaptive) // 적응형 지연을 사용할지 여부
    {
        AdvancePlaybackClock(now, deltaTime, adaptive);
        ApplyDueStateEvents(deltaTime);
        DiscardPlayedSnapshots();
        PlayerSnapshotPose pose = CalculatePose(); // 이번 재생 시각의 위치·각도와 앞 기록
        ApplyPose(pose);
#if UNITY_EDITOR
        UpdateDiagnostics(now, pose);
#endif
    }

    #region 스냅샷 버퍼

    /// <summary>초기 응답 이전 기록을 제거하고 기준 기록을 가장 앞에 둡니다.</summary>
    private void InitializeSnapshotBuffer(PlayerSnapshot snapshot) // 재생 시작 기준이 되는 전체 상태
    {
        initialTime = snapshot.Time;
        // 초기 RPC보다 나중에 발생한 기록은 도착이 먼저였어도 유지한다.
        snapshots.RemoveAll(sample => sample.Time <= initialTime);
        snapshots.Insert(0, snapshot);
    }

    /// <summary>중복 없이 삽입하며 지금까지의 가장 최신 기록이면 true를 반환합니다.</summary>
    private bool InsertSnapshot(
        PlayerSnapshot snapshot) // 수신한 이동 기록
    {
        if (hasInitialState && snapshot.Time <= initialTime)
        {
            return false;
        }

        double previousLatestTime = snapshots.Count > 0 ? snapshots[snapshots.Count - 1].Time : double.NegativeInfinity; // 삽입 전 최신 기록 시각(초)
        int insertIndex = snapshots.Count; // 새 기록이 들어갈 시간순 위치
        while (insertIndex > 0 && snapshots[insertIndex - 1].Time > snapshot.Time)
        {
            insertIndex--;
        }

        bool duplicatesPrevious = insertIndex > 0 && snapshots[insertIndex - 1].Time == snapshot.Time; // 바로 앞 기록과 같은 시각인지 여부
        bool duplicatesNext = insertIndex < snapshots.Count && snapshots[insertIndex].Time == snapshot.Time; // 바로 뒤 기록과 같은 시각인지 여부
        if (duplicatesPrevious || duplicatesNext)
        {
            return false;
        }

        snapshots.Insert(insertIndex, snapshot);
        // 실행 중 용량을 줄여도 다음 삽입에서 새 상한까지 오래된 기록을 제거한다.
        while (snapshots.Count > snapshotCapacity)
        {
            snapshots.RemoveAt(0);
        }

        // 늦게 도착한 기록도 보간에는 쓰되 거리·지터의 최신 기준은 되돌리지 않는다.
        return snapshot.Time > previousLatestTime;
    }

    /// <summary>재생 시각을 지난 기록을 제거하되 마지막 기록 하나는 외삽용으로 남깁니다.</summary>
    private void DiscardPlayedSnapshots()
    {
        while (snapshots.Count > 1 && snapshots[1].Time <= playbackTime)
        {
            snapshots.RemoveAt(0);
        }
    }

    /// <summary>기록이 한 개 이상일 때 앞 기록과 다음 기록을 읽고 보간 가능 여부를 반환합니다.</summary>
    private bool ReadSnapshotPair(
        out PlayerSnapshot from, // 버퍼의 앞쪽 기준 기록; 기록이 부족하면 재생 시각보다 미래일 수 있음
        out PlayerSnapshot to) // 다음 기록; 하나뿐이면 from과 같은 값
    {
        from = snapshots[0];
        to = snapshots.Count > 1 ? snapshots[1] : from;
        return snapshots.Count > 1;
    }

    #endregion

    #region 재생 시계

    // 지연·지터·배율 설정은 매 수신·프레임 계산에서 Inspector 필드를 직접 읽어 현재 조절값을 반영한다.

    /// <summary>전체 초기 응답을 재생 시작점으로 사용하되 먼저 받은 최신 기록 시각은 유지합니다.</summary>
    private void InitializePlaybackClock(
        PlayerSnapshot snapshot, // 초기 전체 상태의 이동 기록
        double arrivalTime) // 이쪽에서 응답을 받은 시각(초)
    {
        playbackTime = snapshot.Time;
        latestSampleTime = Math.Max(latestSampleTime, snapshot.Time);
        lastRate = snapshot.Rate;
        lastArrivalTime = arrivalTime;
    }

    /// <summary>새로운 최신 기록에 대해서만 지터와 전송 등급을 갱신합니다.</summary>
    private void RecordLatestSnapshot(
        PlayerSnapshot snapshot, // 버퍼에서 최신으로 판정한 기록
        double arrivalTime) // 이쪽에서 받은 Photon 시각(초)
    {
        if (double.IsNegativeInfinity(latestSampleTime) || lastRate != snapshot.Rate)
        {
            jitter = 0f;
        }
        else
        {
            double arrivalInterval = arrivalTime - lastArrivalTime; // 실제 수신 간격(초)
            double sampleInterval = snapshot.Time - latestSampleTime; // 송신 측 기록의 발생 간격(초)
            float intervalDifference = (float)Math.Abs(arrivalInterval - sampleInterval); // 네트워크로 인해 변한 간격(초)
            float limitedDifference = Mathf.Min(intervalDifference, maxBufferDelay); // 한 번의 큰 흔들림 영향에 둘 상한(초)
            // 새 지터 = 이전 지터 × (1-반영 비율) + 측정값 × 반영 비율.
            jitter = Mathf.Lerp(jitter, limitedDifference, jitterSmoothing);
        }

        latestSampleTime = snapshot.Time;
        lastArrivalTime = arrivalTime;
        lastRate = snapshot.Rate;
    }

    /// <summary>지연 목표와 재생 속도를 계산하고 시각을 앞으로 진행합니다.</summary>
    private void AdvancePlaybackClock(
        double now, // 현재 Photon 시각(초)
        float deltaTime, // timeScale과 독립적인 프레임 경과 시간(초)
        bool adaptive) // 지터와 전송 등급을 반영할지 여부
    {
        float delay = CalculateBufferDelay(adaptive); // 기록을 확보하기 위해 기다릴 목표 시간(초)
        double targetTime = now - delay; // 화면에서 목표로 삼는 조금 전의 Photon 시각(초)
        double timeDifference = targetTime - playbackTime; // 양수면 재생이 목표보다 뒤처진 정도(초)
        float playbackSpeed = CalculatePlaybackSpeed(timeDifference); // 목표에 천천히 접근할 재생 배율

        double nextTime = playbackTime + deltaTime * playbackSpeed; // 배율을 반영한 다음 재생 시각(초)
        double limitedTime = Math.Min(now, nextTime); // 실제 현재 시각보다 미래로 진행하지 않는 후보 시각
        playbackTime = Math.Max(playbackTime, limitedTime); // 지연 목표가 바뀌어도 시계가 뒤로 가지 않게 제한
#if UNITY_EDITOR
        targetDelay = delay;
        calculatedPlaybackSpeed = playbackSpeed;
        clockTimeDifference = timeDifference;
#endif
    }

    /// <summary>전송 간격과 지터 여유를 더해 목표 지연을 구합니다.</summary>
    private float CalculateBufferDelay(
        bool adaptive) // 전송 간격·지터를 반영할지 선택
    {
        if (!adaptive)
        {
            return fixedBufferDelay;
        }

        float snapshotDelay = bufferedIntervals / lastRate; // 전송 간격(1/Hz)을 여러 번 기다릴 시간(초)
        float jitterDelay = jitter * jitterMargin; // 도착 간격 흔들림에 추가할 여유(초)
        float desiredDelay = snapshotDelay + jitterDelay; // 제한 전 목표 지연(초)
        return Mathf.Clamp(desiredDelay, minBufferDelay, maxBufferDelay);
    }

    /// <summary>뒤처지면 빠르게, 앞서면 느리게 재생하도록 배율을 제한합니다.</summary>
    private float CalculatePlaybackSpeed(
        double timeDifference) // 목표 시각에서 실제 재생 시각을 뺀 값(초)
    {
        float speedCorrection = (float)timeDifference * clockCorrection; // 시각 차이로부터 구한 배율 보정량
        float desiredSpeed = c_normalPlaybackSpeed + speedCorrection; // 정상 배율에 보정을 더한 값
        return Mathf.Clamp(desiredSpeed, minPlaybackSpeed, maxPlaybackSpeed);
    }

    #endregion

    #region 상태 이벤트

    /// <summary>초기 생사·순서 기준을 설정하고 응답에 이미 포함된 변경을 정리합니다.</summary>
    private void InitializeStateEvents(
        PlayerSnapshot snapshot, // 초기 응답의 시각·상태·생사 버전
        int sequence) // 초기 응답이 이미 반영한 마지막 RPC 순서
    {
        initialSequence = sequence;
        appliedLifeVersion = snapshot.LifeVersion;
        appliedItemSequence = sequence;
        isAlive = snapshot.State != PlayerState.Dead;
        for (int typeIndex = 0; typeIndex < appliedSequences.Length; typeIndex++) // 기준을 설정할 이벤트 종류 번호
        {
            appliedSequences[typeIndex] = sequence;
        }
        events.RemoveAll(change => change.Time < initialTime || change.Sequence <= sequence);
    }

    /// <summary>초기·적용 기준보다 새로운 이벤트만 발생 시각과 순서로 삽입합니다.</summary>
    private void InsertStateEvent(PlayerStateEvent stateEvent) // 수신한 확정값 RPC 기록
    {
        int typeIndex = (int)stateEvent.Type; // 이 변경 종류의 마지막 적용 순서를 조회할 위치
        if (hasInitialState)
        {
            bool predatesInitialState = stateEvent.Time < initialTime; // 초기 응답보다 과거에 발생한 변경인지 여부
            bool includedInInitialState = stateEvent.Sequence <= initialSequence; // 전체 응답에 이미 포함된 변경인지 여부
            bool alreadyApplied = stateEvent.Sequence <= appliedSequences[typeIndex]; // 같은 종류의 최신 확정값보다 오래된 순서인지 여부
            if (predatesInitialState || includedInInitialState || alreadyApplied)
            {
                return;
            }
        }

        foreach (PlayerStateEvent pendingEvent in events) // 같은 전역 순서가 이미 대기 중인지 확인할 변경
        {
            if (pendingEvent.Sequence == stateEvent.Sequence)
            {
                return;
            }
        }

        int insertIndex = events.Count; // 발생 시각과 순서에 맞는 삽입 위치
        while (insertIndex > 0 && EventComesAfter(events[insertIndex - 1], stateEvent))
        {
            insertIndex--;
        }
        events.Insert(insertIndex, stateEvent);
    }

    /// <summary>앞 이벤트가 뒤 이벤트보다 나중에 재생되어야 하는지 확인합니다.</summary>
    private static bool EventComesAfter(
        PlayerStateEvent previous, // 삽입 위치 앞에 있는 변경
        PlayerStateEvent incoming) // 새로 삽입할 변경
    {
        bool laterTime = previous.Time > incoming.Time; // 시각 자체가 더 나중인지 여부
        bool laterSequenceAtSameTime = previous.Time == incoming.Time && previous.Sequence > incoming.Sequence; // 같은 시각에서 송신 순서가 뒤인지 여부
        return laterTime || laterSequenceAtSameTime;
    }

    /// <summary>재생 시각에 도달한 변경을 즉시 하나씩 적용하여 부작용 순서를 보존합니다.</summary>
    private void ApplyDueStateEvents(
        float deltaTime) // 지연 진단의 한 프레임 기준 시간(초)
    {
        ProcessDueEvents(deltaTime);
        // 첫 순회에서 생사가 확정되면 앞서 보류한 같은 시각의 표현 상태도 이번 프레임에 적용한다.
        ProcessDueEvents(deltaTime);
    }

    /// <summary>미확정 생사 버전은 보류하고 과거·중복 변경을 제외한 뒤 적용합니다.</summary>
    private void ProcessDueEvents(
        float deltaTime) // 한 프레임의 경과 시간(초)
    {
        bool waitingForItem = !RestoreInitialHeldItem();
        for (int eventIndex = 0; eventIndex < events.Count;) // 제거한 위치의 다음 이벤트를 계속 검사할 인덱스
        {
            PlayerStateEvent stateEvent = events[eventIndex]; // 이번에 검사할 시간순 변경
            if (stateEvent.Time > playbackTime)
            {
                break;
            }

            if (stateEvent.IsItemAction && waitingForItem)
            {
                eventIndex++;
                continue;
            }
            bool waitingForLifeState = !stateEvent.IsItemAction && stateEvent.Type != PlayerStateEventType.LifeState
                && stateEvent.LifeVersion > appliedLifeVersion; // 표시 상태는 새 생사 기준을 기다린다.
            if (waitingForLifeState)
            {
                eventIndex++;
                continue;
            }

            if (stateEvent.IsItemAction && !CanResolveItemEvent(stateEvent))
            {
                waitingForItem = true;
                eventIndex++;
                continue;
            }
            events.RemoveAt(eventIndex);
            int typeIndex = (int)stateEvent.Type; // 종류별 적용 순서의 위치
            bool alreadyApplied = stateEvent.Sequence <= appliedSequences[typeIndex]; // 같은 종류의 더 최신 변경을 적용했는지 여부
            bool oldLifeVersion = !stateEvent.IsItemAction && stateEvent.LifeVersion < appliedLifeVersion; // 확정된 발사·해제는 생사 표시와 별도로 처리한다.
            alreadyApplied |= stateEvent.IsItemAction && stateEvent.Sequence <= appliedItemSequence;
            if (alreadyApplied || oldLifeVersion)
            {
                continue;
            }

            appliedSequences[typeIndex] = stateEvent.Sequence;
            if (stateEvent.IsItemAction)
            {
                appliedItemSequence = stateEvent.Sequence;
            }
#if UNITY_EDITOR
            if (stateEvent.Time < playbackTime - deltaTime)
            {
                lateEventCount++;
            }
#endif
            if (stateEvent.Type == PlayerStateEventType.LifeState)
            {
                if (stateEvent.LifeVersion <= appliedLifeVersion)
                {
                    continue;
                }
                // 생사 기준은 객체 표시 전에 확정한다. 이어지는 손전등 적용은 이 시점의 생존 여부를 읽는다.
                appliedLifeVersion = stateEvent.LifeVersion;
                isAlive = stateEvent.Value;
            }
            ApplyStateEvent(stateEvent);
        }
    }

    /// <summary>채택한 상태 변경을 순서대로 실제 컴포넌트에 적용합니다.</summary>
    private void ApplyStateEvent(PlayerStateEvent stateEvent) // 이번 시각에 채택된 변경 한 개
    {
        switch (stateEvent.Type)
        {
            case PlayerStateEventType.Flashlight:
                flashlight.SetFlashlightEnabled(isAlive && stateEvent.Value);
                break;
            case PlayerStateEventType.HandsUp:
                animationController.SetHandsUp(stateEvent.Value);
                break;
            case PlayerStateEventType.Grab:
                if (playerGrabber != null)
                {
                    playerGrabber.TryGrab(stateEvent.ItemViewId);
                }
                break;
            case PlayerStateEventType.Drop:
                if (playerGrabber != null && playerGrabber.TryDrop(stateEvent.ItemViewId, stateEvent.Position, stateEvent.Rotation))
                {
                    SetItemReleaseContext(stateEvent);
                }
                break;
            case PlayerStateEventType.Shoot:
                if (playerGrabber != null && playerGrabber.TryShoot(stateEvent.ItemViewId, stateEvent.Position, stateEvent.Rotation, stateEvent.Force))
                {
                    SetItemReleaseContext(stateEvent);
                }
                break;
            case PlayerStateEventType.ItemStop:
                PhotonView itemView = PhotonView.Find(stateEvent.ItemViewId);
                Item item = itemView != null ? itemView.GetComponent<Item>() : null;
                if (item != null)
                {
                    item.ApplyPlayerStop(networkSync, stateEvent.ReleaseSequence, stateEvent.Position, stateEvent.Rotation);
                }
                break;
            case PlayerStateEventType.LifeState:
                PlayerState state = isAlive ? PlayerState.Idle : PlayerState.Dead; // 새 생사 버전에서 시작할 표시 상태
                stateMachine.SetState(state);
                flashlight.SetFlashlightEnabled(false);
                break;
        }
    }

    /// <summary>초기 소지 아이템의 생성이 늦어도 짧게 기다린 뒤 현재 소지 상태를 복원합니다.</summary>
    private bool RestoreInitialHeldItem()
    {
        if (initialHeldItemViewId == 0)
        {
            return true;
        }
        if (PhotonView.Find(initialHeldItemViewId) == null && Time.realtimeSinceStartupAsDouble < initialItemDeadline)
        {
            return false;
        }
        if (playerGrabber != null)
        {
            playerGrabber.TryGrab(initialHeldItemViewId);
        }
        initialHeldItemViewId = 0;
        return true;
    }

    /// <summary>아직 생성되지 않은 아이템 때문에 이후 아이템 동작의 순서가 바뀌지 않게 기다립니다.</summary>
    private bool CanResolveItemEvent(PlayerStateEvent stateEvent)
    {
        if (PhotonView.Find(stateEvent.ItemViewId) != null)
        {
            unresolvedItemSequence = 0;
            return true;
        }
        if (unresolvedItemSequence != stateEvent.Sequence)
        {
            unresolvedItemSequence = stateEvent.Sequence;
            unresolvedItemDeadline = Time.realtimeSinceStartupAsDouble + c_itemResolveTimeout;
        }
        if (Time.realtimeSinceStartupAsDouble < unresolvedItemDeadline)
        {
            return false;
        }
#if UNITY_EDITOR
        Debug.LogWarning($"[PlayerSnapshotPlayback] 아이템 ViewID {stateEvent.ItemViewId}를 찾지 못해 동작 {stateEvent.Type}을 건너뜁니다.", this);
#endif
        unresolvedItemSequence = 0;
        return true;
    }

    /// <summary>원격 해제에도 동일한 플레이어·순서를 기록해 과거 정지 보정을 구분합니다.</summary>
    private void SetItemReleaseContext(PlayerStateEvent stateEvent)
    {
        PhotonView itemView = PhotonView.Find(stateEvent.ItemViewId);
        Item item = itemView != null ? itemView.GetComponent<Item>() : null;
        if (item != null)
        {
            item.SetPlayerReleaseContext(networkSync, stateEvent.Sequence);
        }
    }

    #endregion

    #region 자세 계산·표시

    /// <summary>두 기록이 있으면 보간하고 마지막 기록만 남으면 제한된 외삽을 선택합니다.</summary>
    private PlayerSnapshotPose CalculatePose()
    {
        PlayerSnapshot from; // 상태·자세의 기준으로 사용할 앞쪽 기록
        PlayerSnapshot to; // 보간할 다음 기록; 없다면 from과 같은 기록
        bool canInterpolate = ReadSnapshotPair(out from, out to); // 미래 기록까지 확보해 보간할 수 있는지 여부
        if (canInterpolate)
        {
            return PlayerSnapshotPoseCalculator.Interpolate(from, to, playbackTime);
        }
#if UNITY_EDITOR
        if (playbackTime > from.Time)
        {
            underrunFrames++;
        }
#endif
        return PlayerSnapshotPoseCalculator.Extrapolate(from, playbackTime, maxExtrapolation);
    }

    /// <summary>연속 자세를 먼저 적용하고 생사 기준이 맞는 앞 기록의 이산 상태를 반영합니다.</summary>
    private void ApplyPose(PlayerSnapshotPose pose) // 보간·외삽이 계산한 표시 결과
    {
        body.SetPositionAndRotation(pose.position, Quaternion.Euler(0f, pose.yaw, 0f));
        playerLook.SetPitch(pose.pitch);
        PlayerSnapshot source = pose.source; // 섞지 않고 그대로 적용할 상태·웅크림·방향의 기록
        bool matchingLifeVersion = source.LifeVersion == appliedLifeVersion; // 현재 사망·부활과 같은 버전인지 여부
        bool matchingLifeState = (source.State != PlayerState.Dead) == isAlive; // 현재 생존 여부와 기록 상태가 일치하는지 여부
        // 과거 생사의 자세가 새 생사 RPC를 덮지 않게 한다. 위치·각도는 위에서 계속 재생한다.
        if (!matchingLifeVersion || !matchingLifeState)
        {
            return;
        }
        motor.SetCrouching(source.Crouching);
        stateMachine.SetState(source.State);
        animationController.SetMoveDirection(source.Direction);
    }

#if UNITY_EDITOR
    /// <summary>계산 결과를 변경하지 않고 현재 재생 상태를 Inspector 진단값으로 옮깁니다.</summary>
    private void UpdateDiagnostics(
        double now, // 현재 Photon 시각(초)
        PlayerSnapshotPose pose) // 이번 프레임 계산 결과
    {
        playbackDelayMs = (float)(now - playbackTime) * c_millisecondsPerSecond;
        targetDelayMs = targetDelay * c_millisecondsPerSecond;
        playbackSpeed = calculatedPlaybackSpeed;
        extrapolationMs = pose.extrapolationTime * c_millisecondsPerSecond;
        correctionMs = (float)clockTimeDifference * c_millisecondsPerSecond;
        bufferedSnapshots = snapshots.Count;
        pendingEvents = events.Count;
        lateEvents = lateEventCount;
    }
#endif

    #endregion
}

#endif
