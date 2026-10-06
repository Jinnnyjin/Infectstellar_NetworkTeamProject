# Infectstellar 게임 작업 인수인계

최종 갱신일: 2026-10-06 (Asia/Seoul). 경로는 Unity 프로젝트 루트 기준이며, 검증 결과는 아래에 적힌 당시 확인 결과이다.

## 현재 구현 상태

플레이어의 HFSM 상태 관리 코드 구현을 완료했다. UnityHFSM 2.3.0을 사용하며 입력·실제 이동·물리·애니메이션·Photon 연결은 포함하지 않는다.

| 파일 | 역할 |
|---|---|
| [PlayerStateMachine.cs](../../Assets/Scripts/Player/PlayerStateMachine.cs) | 외부 상황 전달, 사망·부활, 현재 상태 조회와 최종 상태 변경 이벤트를 제공하는 컴포넌트 |
| [PlayerStates.cs](../../Assets/Scripts/Player/PlayerStates.cs) | 중첩 HFSM 상태 그래프와 전환 규칙 |
| [PlayerState.cs](../../Assets/Scripts/Enum/PlayerState.cs) | 외부에서 조회하는 최종 상태 enum |

Root의 Alive 아래 Grounded(Idle/Walk/Run/CrouchIdle/CrouchWalk)와 Airborne(Jump/Fall)을 두며, Root의 Dead로 즉시 사망한다. 씬·프리팹은 변경하지 않았고 플레이어 객체에 컴포넌트를 부착하거나 이동 시스템과 연결하지 않았다.

## 주요 API와 결정

- 외부 이동 시스템이 UpdateMovementContext(isMoving, wantsToRun, wantsToCrouch, isGrounded, verticalVelocity)를 호출하면 같은 호출 안에서 최종 상태를 결정한다. 접지 여부와 수직 속도는 호출자가 판단한다.
- 접지 중 웅크리기가 달리기보다 우선한다. 비접지 중 수직 속도가 양수면 Jump, 0 이하면 Fall이다. 착지 시 최신 웅크리기 의도를 전달한다.
- Die는 즉시 Dead로 전환하고 사망 중 이동 상황은 무시한다. Revive는 사망 중에만 이전 상황을 초기화하고 Idle로 복귀한다.
- CurrentState와 IsAlive는 읽기 전용이다. StateChanged(previousState, currentState)는 초기화 알림 없이 최종 상태가 실제로 바뀔 때만 발행한다.
- 그래프는 한 번 생성하고 비활성화·재활성화로 초기화하지 않는다. 이벤트 구독자는 자신의 수명에 맞춰 등록·해제한다.

## 확인한 검증과 한계

2026-10-06 당시 독립 코드 검토에서 확정 오류·회귀 위험이 발견되지 않았다. Unity 6000.3.6f1에서 최종 컴파일이 완료됐고, 순수 관리 코드 실행으로 생존 7개 상태의 336개 전환 조합, 사망 중 상황 무시, 사망·부활·반복 호출과 계층 종료를 확인했다. 5,470개 assertion에서 실패는 없었다.

MonoBehaviour의 공개 이벤트 발행·구독 해제와 활성화 수명에 대한 실행 검증, PlayMode·게임 실행·빌드는 수행하지 않았다. 당시 기존 Collections 테스트 DLL 경로 누락과 Pipeline 통신 오류는 별도 문제로 남았으며, 새 플레이어 코드의 컴파일 오류는 없었다. 이번 인수인계 분리 작업에서는 Unity 검증을 재실행하지 않았다.

설계·당시 검증 근거·남은 범위는 [플레이어 HFSM 작업 계획](plans/2026-10-06-player-hfsm.md)에 둔다.

## 다음 작업

1. 실제 연결 대상 플레이어 객체·씬·프리팹과 이동 시스템을 확인한다.
2. PlayerStateMachine을 부착하고 외부 이동 시스템에서 최신 이동 상황을 전달한다.
3. 이벤트 구독 수명과 컴포넌트 활성화·비활성화 동작을 확인한다.
4. 감정표현 이동 제한과 Photon 네트워크 권한·동기화 정책은 후속 구현 전에 확정한다.

재개 시 실제 코드와 Git 상태를 대조한다. 이 기록은 상태 관리 코드의 완료를 뜻하며 실제 플레이어 이동·네트워크 플레이 검증 완료를 뜻하지 않는다.
