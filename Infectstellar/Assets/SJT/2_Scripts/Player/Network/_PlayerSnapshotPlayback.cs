using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

/// <summary>원격 스냅샷과 RPC를 단조 증가하는 Photon 재생 시계에 맞춰 표시합니다.</summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class _PlayerSnapshotPlayback : MonoBehaviour
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

    /// <summary>전체 초기 상태를 확인한 뒤에만 원격 표시를 진행합니다.</summary>
    public bool HasInitialState => hasInitialState;

    /// <summary>매 프레임 재사용할 컴포넌트와 로컬·원격 역할을 연결합니다.</summary>
    public void Initialize(PlayerNetworkSync _networkSync, PlayerStateMachine _stateMachine, PlayerMotor _motor, PlayerLook _playerLook, FlashlightController _flashlight, PlayerAnimationController _animationController, bool _isRemote)
    {
        networkSync = _networkSync;
        stateMachine = _stateMachine;
        motor = _motor;
        playerLook = _playerLook;
        flashlight = _flashlight;
        animationController = _animationController;
        isRemote = _isRemote;
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

    private void ApplyPose(PlayerSnapshot from, PlayerSnapshot to, float t, Vector3 position)
    {
        //body.SetPositionAndRotation();
    }
}
