# Infectstellar 게임 작업 인수인계

최종 갱신일: 2026-10-10 (Asia/Seoul). 경로는 Unity 프로젝트 루트 기준이다.

## 현재 상태: Player 입력·회전 분리와 임시 1인칭 카메라

사용자가 승인한 [입력·회전 계획](plans/2026-10-10-player-input-look.md)에 따라 PlayerScene 인스턴스의 Unity PlayerInput, PlayerInputReader, PlayerLook과 Cinemachine 카메라를 연결했다. Controller는 입력 적재 → 회전 → 기존 상태머신 Tick을 시작한다. 상세 구조는 [PROJECT.md](PROJECT.md)의 PlayerScene 절을 참고한다.

- 기존 이동 정책·수치와 사용자 재작성본/비교 파일을 보존했다. Settings 상속, HFSM root.Init(), 씬의 타입/설정 연결과 과거 Motor 참조를 최소 복구했다. 실행에서 발견한 기존 점프 차단은 Motor의 바닥 검사에 CharacterController 접촉 여유를 포함해 보완했다.
- 새 코드 학습 시 PlayerInputReader의 CurrentFrame/LookDelta → PlayerLook.Rotate → PlayerController.Update → 기존 Tick 흐름을 연결해서 설명한다. 예전 Controller의 입력 자산 복제·액션 수명 설명은 현재 구조에 적용하지 않는다.
- 독립 리뷰에서 재잠금 직후 새 점프 누락을 발견해 실제 버튼 홀드 스냅샷으로 수정했다. 수정본 재검토에서 추가 확정 오류는 없었다.
- 최종 Unity 컴파일 성공, PlayMode 임시 검사 40/40 통과, 신규 Console 오류 0. 입력 이벤트/포커스 콜백 시뮬레이션을 사용했으므로 실제 조작감·OS 창 전환은 수동 확인이 남는다. 세부 근거와 확인 절차는 위 계획에 있다. Play는 정지했고 임시 설정을 복구했다. 저장 후 Editor에 생긴 dirty 상태는 보존했다.
- 남은 기능은 웅크림 시점 높이, 모델 애니메이션, 네트워크 소유권/동기화다. 이번에는 고정 머리 높이, 자기 모델 표시, 로컬 키보드·마우스 조작이다.

## 이전 학습 기록 (2026-10-08): Player 이동 코드 수동 재작성

사용자는 기존 Player 이동 코드를 처음부터 직접 따라 치며 이해하는 중이다. 다음 채팅에서도 아래 학습 순서와 설명 방식을 유지한다. 이번 요청은 학습 인수인계 기록이며 게임 코드 수정·컴파일 요청이 아니다. 설명 질문에서는 파일을 수정하지 않고, 별도 실행 요청이 있을 때만 수정한다.

### 따라 작성할 파일 순서와 이유

전체 원칙은 **상태 이름·설정·전달 자료형 → 실제 이동 → 상태 판단 → 두 기능의 연결 → 입력 진입점**이다. 뒤의 파일을 읽을 때 등장하는 사용자 정의 타입과 역할을 먼저 알아두기 위한 학습 순서이며, 런타임 호출 순서나 각 단계의 컴파일 성공을 보장하는 순서는 아니다. Motor와 그래프는 설정·자료형을 공유하지만, 그래프가 Motor를 직접 사용하는 구조는 아니다.

| 순서 | 파일 | 먼저 공부할 내용 | 이 순서인 이유 |
|---|---|---|---|
| 1 | [PlayerState.cs](../../Assets/SJT/2_Scripts/Enum/PlayerState.cs) | Idle·Walk·Run·CrouchIdle·CrouchWalk·Jump·Fall·Dead 이름 | 이후 코드에서 사용하는 공개 상태 이름부터 익힌다. 상위 계층 이름은 그래프 내부 enum으로 구분한다. |
| 2 | [PlayerMovementSettings.cs](../../Assets/SJT/2_Scripts/SO/PlayerMovementSettings.cs) | 이동 속도·점프 높이·중력·웅크린 높이 비율, 필드와 프로퍼티 | Motor와 상태 판단에서 읽는 공통 설정을 먼저 이해한다. |
| 3 | [PlayerInputFrame.cs](../../Assets/SJT/2_Scripts/Struct/PlayerInputFrame.cs) | 한 프레임의 Move·RunHeld·CrouchHeld·JumpPressed | 입력을 어떤 자료형으로 모아서 전달하는지 이해한다. 입력 수집 자체는 마지막 Controller에서 다룬다. |
| 4 | [PlayerMovementCommand.cs](../../Assets/SJT/2_Scripts/Struct/PlayerMovementCommand.cs) | 상태 판단 결과인 이동 방향·속도·점프·웅크림 명령 | 입력과 실행 명령의 차이, HFSM이 결정하고 Motor가 실행하는 경계를 익힌다. |
| 5 | [PlayerMotor.cs](../../Assets/SJT/2_Scripts/Player/PlayerMotor.cs) | 명령을 받아 CharacterController 이동·중력·점프·충돌·캡슐 변경 | 상태 정책을 공부하기 전에 명령이 실제 몸에 어떤 영향을 주는지와 접지·수직 속도 등 결과를 이해한다. |
| 6 | [PlayerStateGraph.cs](../../Assets/SJT/2_Scripts/Player/PlayerStateGraph.cs) — 최초 안내명 `PlayerStates.cs` | HFSM 계층·상태 등록·전환 조건·상태별 이동 명령 | 앞의 입력·설정·명령과 몸 정보를 바탕으로 무엇을 결정하는지 이해한다. 가장 복잡한 단계이므로 작은 메서드 단위로 설명한다. |
| 7 | [PlayerStateMachine.cs](../../Assets/SJT/2_Scripts/Player/PlayerStateMachine.cs) | 그래프와 Motor 연결, 외부 API·상태 변경 이벤트 | 두 구성 요소의 역할을 안 뒤 명령 생성 → 실제 이동 → 이동 후 상태 갱신의 실행 순서를 배운다. |
| 8 | [PlayerController.cs](../../Assets/SJT/2_Scripts/Player/PlayerController.cs) | 입력 수집·런타임 입력 인스턴스 수명·컴포넌트 초기화·프레임 시작 | 마지막에 입력이 전체 흐름을 어떻게 시작하는지 연결한다. 처음부터 보면 입력·초기화·상태·물리를 동시에 따라가야 해서 어렵다. |

2026-10-08 파일 확인: 현재 그래프 클래스/파일은 `PlayerStateGraph`이며 `PlayerStates.cs`는 없다. `_PlayerStates.cs`도 별도로 존재하지만 현재 `PlayerStateMachine`이 생성하는 것은 `PlayerStateGraph`다. `_PlayerMovementSettings.cs`, `_PlayerInputFrame.cs` 등 별도 파일과 사용자 재작성 파일을 임의 삭제·이동·원복하지 않는다. 이 학습 순서만으로 앞 단계 작성 완료를 추정하지 않는다.

### 합의한 구조와 이름

- 사용자는 우선 **1번 방식인 AddState의 onEnter/onLogic 콜백 등록**을 유지하기로 했다. 상태별 객체 + PlayerStateContext(2번)는 필요해질 때 재검토한다. 별도 상태 클래스·Context 도입 예시는 대화상의 대안이며 현재 구현 방향으로 간주하지 않는다.
- Controller는 입력과 초기화, StateMachine은 외부 API와 실행 순서, Graph는 상태 계층·전환·명령 결정, Motor는 실제 이동을 담당한다.
- 기존 `Prepare()`의 설명 이름은 `CreateMovementCommand()`, 기존 `Complete()`는 `UpdateStateAfterMovement()`로 변경했고 현재 그래프와 StateMachine 호출부에서도 확인했다.

```text
Controller: 입력 수집 → PlayerStateMachine.Tick(input, deltaTime)
  → Motor.RefreshGrounded(): 이동 전 접지 갱신
  → Graph.CreateMovementCommand(): 입력·몸 정보 저장 → root.OnLogic() → 명령 반환
  → Motor.Simulate(): 실제 이동·중력·충돌 처리 한 번
  → Graph.UpdateStateAfterMovement(): 점프 입력/명령 초기화 → 최신 몸 정보 저장 → root.OnLogic()
  → Motor.SetCrouching(Graph.WantsCrouch): 착지 후 자세 반영
  → 최종 상태 변경 시 StateChanged 발행
```

이동 전에는 어떻게 움직일지 결정하고, 이동 후에는 그 이동에서 발생한 착지·낙하·천장 충돌 결과를 같은 프레임의 상태에 반영한다. root.OnLogic()은 두 번 호출될 수 있지만 실제 이동은 한 번이다. 상태 콜백은 명령 설정을 담당하며 이동을 직접 실행하지 않는다.

### 지금까지 설명한 내용과 다음 설명 방식

- 사용자는 메서드 이름과 데이터 흐름을 구체적인 코드에 연결해야 이해하기 쉽다. 용어만 반복하기보다 현재 코드 위치, 호출 → 변수 변경 → 다음 호출의 흐름을 Walk·점프·착지 예시로 설명한다. 주석 요청에는 바로 붙여 넣을 수 있는 쉬운 한국어 주석을 제공하고, 메서드 역할 요약은 2~3줄 이내로 작성한다.
- 최근 질문: StateMachine 제네릭 타입 인수, AddState/AddTransition 인자와 콜백, OnEnter/OnLogic/OnExit, 조건 전환과 트리거 전환, 이동 전후 두 번 판단하는 이유, 설정 검증 메서드. 마지막 질문은 AddState의 인자별 설명이었다. 설정·입력 파일로 돌아가 다시 공부할 수 있으므로 진도를 완료로 단정하지 않는다.
- `OnLogic()`은 UnityHFSM 메서드이며 Unity가 자동으로 호출하지 않는다. 상태머신은 자기 전환 조건을 확인하고 활성 자식의 OnLogic으로 이어진다. 모든 상태를 검색하거나 안정될 때까지 무한 반복하는 방식이 아니다. 일반 상태 유지 시 Enter/Exit는 재실행되지 않고, 전환 시 기존 Exit → 새 Enter → 새 상태 Logic으로 이어진다. 최초 시작 상태 진입에는 초기화가 필요하다.
- `TEvent = LifeEvent`는 Trigger에 전달할 값의 타입을 지정한다. Die/Revive 동작은 별도의 AddTriggerTransition 등록이 있어야 한다. 현재 root에만 Die/Revive 트리거 전환이 있고 하위 FSM들은 입력·몸 정보의 조건 전환을 사용한다. 같은 TEvent의 활성 자식으로 트리거가 전달될 수 있지만, 부모에서 전환하면 전달은 종료된다. TEvent 일치 자체가 자식 AddState 등록의 필수 조건인 것은 아니다.
- AddState는 상태 객체를 전달하는 2인자 형태와 콜백을 전달하는 최대 7인자 형태를 구분한다. AddTransition의 세 번째 람다는 condition 이름을 생략해 위치로 전달한 조건 함수다. condition은 전환 상황 판단, canExit는 needsExitTime 사용 시 종료 허용 판단이다.
- `IsValid()` 유지·`IsNonNegative()` 제거 방식은 대화에서 코드 예시를 제공했으며 자동 수정 요청은 없었다. 이번 확인 시 설정 파일에는 두 메서드가 모두 남아 있다. 제안 코드를 실제 적용 사실로 기록하지 않는다.

다음 채팅의 시작 요청 예: “HANDOFF의 Player 학습 인수인계를 읽고, 1~8번 순서와 콜백 방식 구조를 유지해서 설명해 줘. 파일은 수정하지 말고 내가 질문한 부분을 현재 코드와 연결해 설명해 줘.”

### 현재 상태와 검증의 한계

사용자가 소스와 .meta를 직접 수정하며 재작성 중이다. 이번 작업은 문서만 변경하며 기존 코드·씬·SO·GUID를 수정하지 않는다. 현재 파일 위치·호출명은 읽기 전용으로 확인했으나 현재 재작성본의 Unity 컴파일·동작 검증은 수행하지 않았다. 아래 구현·검증 기록은 수동 재작성 전의 이력이므로 현재 코드의 통과 근거로 사용하지 않는다.

## 이전 구현 기준: 수동 재작성 전

PlayerScene의 Player에 기본 이동 코드를 연결했다. UnityHFSM 2.3.0과 Input System 1.18.0을 사용한다.

- PlayerController: 입력 자산 복제·수명 관리와 프레임 시작.
- PlayerStateMachine / PlayerStates: Alive → Grounded(Idle/Walk/Run/CrouchIdle/CrouchWalk), Airborne(Jump/Fall), 별도 Dead 상태와 행동 정책.
- PlayerMotor: CharacterController 이동·중력·접지·천장 판정·웅크림 캡슐.
- PlayerMovementSettings SO: 걷기/달리기/웅크리기 3/5/1.5m/s, 점프 높이 1.2m, 중력 20m/s², 웅크린 높이 60%.

코드는 Assets/SJT/2_Scripts 아래 Player·SO·Struct·Enum에 있다. 설정 자산은 Assets/SJT/5_Data/PlayerMovementSettings.asset, 연결 씬은 Assets/SJT/4_Scenes/PlayerScene.unity다. 현재 Player 인스턴스에만 컴포넌트를 추가했으며 Player.prefab 원본은 수정하지 않았다. Animator Root Motion은 껐다.

## 주요 연결과 남은 범위

- 기존 Assets/InputSystem_Actions.inputactions의 Player/Move·Sprint·Crouch·Jump를 사용한다. Shift/C 홀드, 서 있는 지상에서만 Space 신규 입력으로 점프한다.
- 상태 콜백은 명령만 만들고 Motor가 프레임당 한 번 이동한다. 이동 후 상태·착지 웅크림을 보정한 다음 StateChanged를 발행한다.
- CurrentState·IsAlive·StateChanged·Die·Revive는 유지했다. 이전 UpdateMovementContext는 Controller 내부 Tick 흐름으로 대체했다.
- 공중 방향 조절은 이륙 시 속도 기준을 유지한다. 사망 중에는 수평 입력과 새 점프를 차단하고 중력은 유지한다. 부활 다음 갱신에 실제 접지를 반영한다.
- 카메라 회전·추적, 모델 애니메이션, Photon 권한·동기화는 미구현이다. 모델은 T 포즈이며 웅크리기는 충돌 캡슐만 변경한다.
- 사용자 SJT 폴더 이동·삭제 및 SampleScene 기존 변경은 보존한다. 게임 저장소 커밋·푸시는 하지 않았다.

## 검증과 다음 확인

독립 리뷰의 활성화 시 Sprint/Crouch 홀드 누락과 천장 검사 여유 문제를 보완했고 최종 재검토에서 추가 확정 오류는 없었다. 2026-10-07 17:54:44 KST 최종 Unity 컴파일은 완료·성공했으며 새 오류 0건이었다. 씬/설정/스크립트 참조도 확인했다. 검증 근거와 한계는 `docs/ai/plans/2026-10-07-player-movement.md`(현재 파일 없음)의 최신 기록을 따른다. 씬 연결은 저장했으며 이후 다시 표시된 Editor의 미저장 상태는 임의 저장·리로드하지 않고 보호했다.

PlayMode·게임 실행·빌드는 수행하지 않는다. 다음으로 계획의 수동 확인 목록에 따라 입력·점프·천장·낙하·착지·사망·부활·재활성화를 확인한다. 이후 카메라/애니메이션/네트워크는 요구사항을 정한 뒤 연결한다.

2026-10-06의 상태 관리 전용 구현과 과거 5,470 assertion 결과는 [이전 HFSM 계획](plans/2026-10-06-player-hfsm.md)에 남아 있다. 해당 결과를 이번 이동 코드의 런타임 검증으로 사용하지 않는다.

<!-- harness:status:start -->
## 하네스 적용 상태
- 적용한 원본 버전: `1.1.1`
- 기본 경로·버전·직접 패키지·주요 폴더를 확인했다.
- 파일 배치·구조 검사와 실제 훅 신뢰·새 세션 스킬/역할 로딩은 구분한다.
- 게임 컴파일·실행은 이 적용 작업의 확인 대상이 아니다.
<!-- harness:status:end -->
