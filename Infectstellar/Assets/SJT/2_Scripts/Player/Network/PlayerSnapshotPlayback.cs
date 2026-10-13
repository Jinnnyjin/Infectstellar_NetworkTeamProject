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
        if (!double.IsNegativeInfinity(latestSampleTime) && lastRate == snapshot.Rate)
        {
            float variation = (float)Math.Abs((arrivalTime - lastArrivalTime) - (snapshot.Time - latestSampleTime));
            jitter = Mathf.Lerp(jitter, Mathf.Min(variation, 0.3f), 0.1f);
        }
        else
        {
            jitter = 0f;
        }
        latestSampleTime = snapshot.Time;
        lastArrivalTime = arrivalTime;
        lastRate = snapshot.Rate;
        return true;
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
        float delay = adaptive ? Mathf.Clamp(2f / lastRate + jitter * 2f, 0.08f, 0.3f) : 0.1f;
        double error = now - delay - playbackTime;
        float speed = Mathf.Clamp(1f + (float)error * 0.5f, 0.95f, 1.05f);
        playbackTime = Math.Max(playbackTime, Math.Min(now, playbackTime + deltaTime * speed));
        ProcessEvents();
        while (snapshots.Count > 1 && snapshots[1].Time <= playbackTime)
        {
            snapshots.RemoveAt(0);
        }
        PlayerSnapshot from = snapshots[0];
        PlayerSnapshot to = snapshots.Count > 1 ? snapshots[1] : from;
        float t = to.Time > from.Time ? Mathf.Clamp01((float)((playbackTime - from.Time) / (to.Time - from.Time))) : 0f;
        float extrapolation = snapshots.Count == 1 ? Mathf.Clamp((float)(playbackTime - from.Time), 0f, c_maxExtrapolation) : 0f;
        Vector3 position = snapshots.Count > 1 ? Vector3.Lerp(from.Position, to.Position, t) : from.Position + from.Velocity * extrapolation;
        ApplyPose(from, to, t, position);
#if UNITY_EDITOR
        playbackDelayMs = (float)(now - playbackTime) * 1000f;
        targetDelayMs = delay * 1000f;
        playbackSpeed = speed;
        extrapolationMs = extrapolation * 1000f;
        correctionMs = (float)error * 1000f;
        bufferedSnapshots = snapshots.Count;
        pendingEvents = events.Count;
        if (snapshots.Count == 1 && playbackTime > from.Time)
        {
            underrunFrames++;
        }
#endif
    }

    /// <summary>생사 버전이 확정되지 않은 변경은 보류하고 늦은 RPC도 시간 역행 없이 한 번 적용합니다.</summary>
    private void ProcessEvents()
    {
        // 생사 확정 RPC를 처리한 뒤 같은 시각의 다른 상태가 한 프레임 더 지연되지 않게 두 번 순회한다.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < events.Count;)
            {
                PlayerStateEvent e = events[i];
                if (e.Time > playbackTime)
                {
                    break;
                }
                if (e.Type != PlayerStateEventType.LifeState && e.LifeVersion > appliedLifeVersion)
                {
                    i++;
                    continue;
                }
                events.RemoveAt(i);
                int typeIndex = (int)e.Type;
                if (e.Sequence <= appliedSequences[typeIndex] || e.LifeVersion < appliedLifeVersion)
                {
                    continue;
                }
                appliedSequences[typeIndex] = e.Sequence;
#if UNITY_EDITOR
                if (e.Time < playbackTime - Time.unscaledDeltaTime)
                {
                    lateEvents++;
                }
#endif
                switch (e.Type)
                {
                    case PlayerStateEventType.Flashlight:
                        flashlight.SetFlashlightEnabled(isAlive && e.Value);
                        break;
                    case PlayerStateEventType.HandsUp:
                        animationController.SetHandsUp(e.Value);
                        break;
                    case PlayerStateEventType.LifeState:
                        if (e.LifeVersion <= appliedLifeVersion)
                        {
                            break;
                        }
                        appliedLifeVersion = e.LifeVersion;
                        isAlive = e.Value;
                        stateMachine.ApplyRemoteState(isAlive ? PlayerState.Idle : PlayerState.Dead);
                        flashlight.SetFlashlightEnabled(false);
                        break;
                }
            }
        }
    }

    /// <summary>움직임은 보간하고 이산 자세는 같은 생사 버전의 이전 스냅샷 시점에 적용합니다.</summary>
    private void ApplyPose(PlayerSnapshot from, PlayerSnapshot to, float t, Vector3 position)
    {
        body.SetPositionAndRotation(position, Quaternion.Euler(0f, Mathf.LerpAngle(from.Yaw, to.Yaw, t), 0f));
        playerLook.ApplyRemotePitch(Mathf.LerpAngle(from.Pitch, to.Pitch, t));
        if (from.LifeVersion != appliedLifeVersion || (from.State != PlayerState.Dead) != isAlive)
        {
            return;
        }
        motor.ApplyRemoteCrouch(from.Crouching);
        stateMachine.ApplyRemoteState(from.State);
        animationController.SetMoveDirection(from.Direction);
    }
}
