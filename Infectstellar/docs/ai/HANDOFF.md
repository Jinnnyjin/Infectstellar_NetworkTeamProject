# Infectstellar 게임 작업 인수인계

최종 갱신일: 2026-10-12 (Asia/Seoul). 경로는 Unity 프로젝트 루트 기준이다.

## 현재 상태: 손전등 제어와 R키 연결

- FlashLightSetter를 FlashlightController로 이름 변경하고 기존 GUID를 보존했다. 최초 꺼짐, R키 신규 입력 반전, Inspector의 두 표면 색, 사망 즉시 끄기·입력 차단, 부활 꺼짐 유지, 일반 재활성화 상태 유지와 변경 이벤트를 구현했다. Light.enabled와 모델의 헬멧 슬롯 2에 MaterialPropertyBlock을 사용한다.
- 독립 리뷰·최종 Unity 컴파일·타입 반영 확인·하네스 구조 검사를 통과했다. 새 컴파일 오류 0건이며 기존 카메라 스크립트 경고 2건이다. 프리팹·씬·Material·Input Actions는 수정하지 않았다. Player 루트에 컴포넌트를 추가하고 ViewPivot/Flashlight의 Light, Player_TPose_Unrigged의 SkinnedMeshRenderer, 슬롯 2와 두 색을 직접 연결해야 한다. Inspector 연결·Play는 미검증이며 상세 결과는 [손전등 제어 계획](plans/2026-10-12-flashlight-controller.md)에 둔다.

## 현재 상태: HandsUp 호출 API 추가

- PlayerAnimationController에 `SetHandsUp(true/false)`를 추가했다. Arms 레이어의 적용 Weight를 기존 BlendDuration으로 전환하고, 켜질 때만 HandsUp을 처음부터 재생한다. 활성화/비활성화 때 적용은 꺼진 상태로 초기화한다. 이동 Base Layer와 독립적이며 입력·사망·네트워크 호출은 별도 연결이 필요하다.
- 사용자 구성 Animator·마스크·클립·씬·프리팹은 수정하지 않았다. 독립 코드 검토와 하네스 구조 검사는 통과했다. 전체 Unity 컴파일은 병행 작성 중인 `Assets/SJT/2_Scripts/Player/Input/FlashLightSetter.cs`의 Color 타입 충돌(CS0104, 6·7행)과 Action<> 미정의(CS0246, 11행)로 실패했으며 해당 파일은 수정하지 않았다. 최종 HandsUp의 실행 사례와 실제 이동 중 팔 자세·전환 느낌은 미검증이다.

## 이전 작업: PlayerScene 기본 손전등

- Cookie 없는 흰색 기본 Spot Light와 약한 환경광·어두운 카메라 배경을 씬에 적용했다. 광원의 로컬 Z 0.2 이동으로 카메라 앞 Player 표면의 과한 근접 반사를 피한다. Player 프리팹·입력·모델·기존 사용자 변경은 보존했다.
- 독립 리뷰와 최종 저장본 Play 검증 36개 assertions를 통과했다. 기존 Player 모델의 화면 하단 가림, 10~15m 벽/물체 식별과 Mobile/성능은 남은 확인이다. 단계·완료 근거·로그/병행 변경의 한계는 [손전등 계획](plans/2026-10-12-player-flashlight.md)에 둔다.

## 현재 상태: Animator 직접 관리 전환

- PlayerAnimationController의 런타임 Override 생성·Controller 복원과 클립 대체를 제거하고 Animator에 연결된 Controller를 그대로 사용한다. 상태별 해시·HFSM 직접 호출·동일 상태 재시작 방지·0.15초 완화를 유지한다.
- 공유 PlayerController.controller의 8개 상태/3개 방향 Blend Tree에 기존 실제 클립 17개를 직접 연결했다. Settings에는 전환 시간만 남는다. 이후 클립 변경은 Animator에서 한다. 기존 GUID·프리팹/씬/백업·실제 클립 파일을 보존했다.
- 독립 리뷰·최종 Unity 컴파일·직접 Motion 연결 검사 170개 조건을 통과했다. 새 오류 0건이며 대상 밖 기존 경고 6건이다. 단계·결과·Play 미검증 항목은 [Animator 직접 관리 계획](plans/2026-10-12-player-animator-direct.md)을 따른다. 기존 Walk 시작 부분 반복 증상의 해결은 아직 확인하지 않았다.

## 현재 상태: 웅크리기 카메라 높이 연동 완료

- Motor가 실제 캡슐 자세 변경 후 Look에 줄어든 높이를 전달하고, Look이 기존 ViewPivot을 DOTween으로 양방향 0.2초 이동한다. HeadLookPoint 연결과 기존 사용자 캡슐/씬 Override·비율 0.65를 유지한다. Controller와 백업은 변경하지 않았다.
- 독립 검토·최종 Unity 컴파일·읽기 전용 프리팹/씬 참조 검증을 통과했다. 새 컴파일 오류 0, 기존 파일 경고 6이다. 실제 Play의 전환 느낌·머리 자세는 사용자가 확인한다. 상세 범위·결과·수동 확인은 [카메라 높이 계획](plans/2026-10-12-player-crouch-camera.md)을 따른다.

## 현재 상태: _Health 학습 원본 단순화

- `Assets/SJT/2_Scripts/Entity/_Health.cs`에서 Queue<Action>·NotifyChange·드레인 플래그·캡처 람다를 제거했다. 각 변경 메서드가 이벤트를 직접 호출한다. 사용자가 작성 중인 같은 폴더의 Health.cs와 DamageInfo, 기존 GUID·Player·씬은 보존했다.
- 초기화·피해·회복·이벤트 계약과 0 피해 피격 알림은 유지한다. 이벤트 중 같은 _Health를 변경하는 호출은 가드로 무시하며 대기 처리하지 않는다. 외부 부활 준비 후 초기화는 이벤트 알림이 끝난 뒤 호출한다.
- 독립 리뷰와 최종 소스의 Unity 참조 인메모리 컴파일·PreviewScene 격리 16개 시나리오/65개 assertions 통과. 전체 프로젝트 컴파일은 학습 중 Health.cs의 기존 미정의 메서드 3건(CS0103)으로 실패했으며 해당 파일을 수정하지 않았다. 입력검사에서 예상 오류 로그 19건이 Console에 남았다. 최종 근거·한계는 _Health 단순화 계획 (`docs/ai/plans/2026-10-11-health-refactor.md`, 현재 파일 없음)을 따른다. 최초 FIFO/클로저 검증 결과는 현재 원본의 근거로 재사용하지 않는다.

## 이전 작업: HFSM의 애니메이션 직접 호출

- HFSM은 SetState, PlayerController는 SetMoveDirection을 호출한다. 애니메이션의 HFSM/InputReader 참조와 이벤트 구독을 제거하고 기존 Animator/17개 클립/0.15초 완화 구조를 유지했다.
- 현재 Player.prefab의 두 호출자를 모델 애니메이션에 연결했다. PlayerScene은 이를 상속한다. 사용자 선택으로 Player_Pre와 _Recovery 백업은 수정하지 않았으며 재사용 시 새 참조 연결이 필요하다.
- 독립 리뷰에서 취소된 같은 프레임 전환의 재시작 문제를 수정하고 재검토했다. 최종 Unity 컴파일과 격리 Editor 검사 37건이 통과했다. 실제 Animator 평가·조작·발 타이밍·PlayMode·빌드는 미검증이다. 상세 단계·결과·검사 한계는 직접 호출 계획 (`docs/ai/plans/2026-10-11-player-animation-push.md`, 현재 파일 없음)을 따른다.

## 이전 작업: Player 클립 이름·독립 복제본·SO 연결

- Player 폴더의 17개 FBX 임포트 클립 이름을 각 FBX 이름으로 바꾸고, 편집 가능한 독립 `.anim` 복제본 17개를 `Assets/SJT/1_Assets/Animations/Clips/`에 만들었다. SO의 17개 필드에 복제본을 지정해 이전 Missing/빈 슬롯을 교체했다.
- 원본 FBX·리그·반복·프레임 구간·Controller·0.15초 전환 시간·씬·프리팹·코드는 유지했다. 실제 캐릭터 재생 자세와 발 미끄러짐은 미검증이다. 단계와 검증 결과는 클립 복제·연결 계획 (`docs/ai/plans/2026-10-11-player-animation-clips.md`, 현재 파일 없음)에 기록한다.
- 독립 검토와 Unity 객체·곡선·설정·SO 참조 검사를 통과했다. 격리된 Editor PreviewScene에서 원본/복제본 17쌍을 4시점씩 비교한 68쌍의 자세 차이는 0이었다. 실제 PlayerScene에서 입력·HFSM 전환·발 미끄러짐을 보는 게임 실행 검증은 하지 않았다.

## 이전 상태: Player FBX의 Humanoid·반복 설정

- `Assets/SJT/1_Assets/Animations/Player/`의 17개 FBX를 Humanoid로 설정하고 원본 계층의 필수 뼈를 직접 매핑했다. 대기·이동 14개는 반복, Jumping_Up·Jumping_Down·Die 3개는 비반복이다. FBX 원본과 구간·클립 이름, SO·Controller·씬·프리팹은 이번 작업에서 수정하지 않았다.
- 새 Crouch_Idle이 있어 현재 슬롯 기준 필요한 17개 동작이 모두 준비됐다. 교체한 Run_Backward/Crouch_Walk_Backward의 원본 수평 누적 이동이 해소됐다. Jumping_Down은 낙하 자세로 사용하며 착지·웅크리기/일어서기 전환·대각선 클립은 추가하지 않는 사용자 결정을 따르기로 했다.
- 다음 연결 작업은 PlayerAnimationSettings.asset의 기존 전진 걷기 Missing 참조를 Walk_Forward로 교체하고, 비어 있는 15개 슬롯에 해당 클립을 지정하는 것이다. 현재 Idle만 새 폴더의 클립에 연결돼 있다. 이 작업에서는 슬롯 등록을 설명만 했으며 변경하지 않았다. 실제 캐릭터 재생·리타기팅 자세·발 미끄러짐은 미검증이다.
- 최종 독립 검토와 Unity 임포트 객체 검사를 통과했다. 17개 모두 유효한 Humanoid Avatar와 단일 Humanoid 클립, 저장/실제 매핑 및 반복 설정이 일치했다. 기존 GUID·클립 fileID·프레임 구간·길이·60fps와 SO·씬·프리팹·FBX 원본을 보존했다. 초기 자동 매핑의 LeftFoot 오류는 각 FBX의 실제 스켈레톤을 유지하고 15개 뼈를 재지정해 해결했다. PlayMode·빌드·게임 코드 컴파일 검사는 수행하지 않았다.

## 이전 상태: 독립 Health 시스템 추가

- 최초 독립 Health 컴포넌트와 DamageInfo를 추가한 당시 기록이다. 당시 이벤트 중 변경 요청은 현재 알림 묶음 종료 후 FIFO로 처리했다. 사용자가 파일을 Entity로 이동하고 원본을 _Health로 구분한 후 큐를 제거했다. 최신 동작은 위 단순화 절을 따른다.
- 최종 Unity 컴파일과 독립 리뷰, PreviewScene 격리 26개 시나리오/126개 assertions를 통과했다. 기존 PlayerScene의 미저장 상태를 보존했으며 PlayMode·게임 실행·빌드는 수행하지 않았다. 상세 계약과 검증 근거는 Health 계획 (`docs/ai/plans/2026-10-11-health.md`, 현재 파일 없음)에 기록했다.
- 최초 작업에서는 씬·프리팹에 연결하지 않았다. 부활 준비·사망 반응·Player/HFSM·네트워크 연결은 외부 작업이다.

## 이전 상태: Player와 모델 분리

- PlayerScene의 새 Player 루트에는 이동·입력·상태·시점을, 자식 Astronaut에는 Animator와 PlayerAnimationController를 구성했다. Inspector 참조·새 카메라 대상·머리 AimConstraint를 연결했다. 프리팹 원본과 FBX 설정은 변경하지 않았다.
- 사용자 설정의 AstronautAvatar는 유효한 Humanoid로 확인했다. PlayerAnimationController만 Inspector 직접 참조와 분리 계층 검사로 최소 수정했다.
- 추가 승인으로 기존 Controller의 잘못된 Idle/전진 걷기 슬롯을 복구하고 선택한 실제 클립은 SO에 연결했다. 현재 Walk.fbx는 비 Humanoid라 기존 정책대로 Idle로 대체한다.
- 구성·Unity 컴파일·독립 리뷰와 실제 입력 경로의 이동·웅크림·점프·시점·카메라·머리 추적·모델 뼈 평가 검증을 완료했다. Play를 종료한 뒤 씬의 Player_Pre를 삭제하고 저장했다. 프리팹 원본은 유지하며 PlayerScene은 dirty=false다. 상세 근거는 모델 분리 계획 (`docs/ai/plans/2026-10-11-player-model-separation.md`, 현재 파일 없음)을 따른다.
- 현재 모든 실제 재생 클립은 Idle로 대체된다. 이동/공중 Animator 상태 전환은 검증했지만, 별도 보행·점프 동작을 보려면 SO에 호환 Humanoid 클립을 등록해야 한다. 실제 Crouch 키는 현재 PlayerControls의 Ctrl이다.

## 이전 상태: Astronaut 원본 리그 비교

- PlayerScene의 Astronaut에 Player와 같은 컴포넌트·Inspector 연결을 적용했다. 자체 컴포넌트·시점·머리 제약 참조는 Astronaut 내부로 연결하고 충돌 캡슐은 스케일 차이를 환산했다. 기존 Player 활성 상태, 시네머신 카메라 설정과 추적 대상은 유지했다.
- 모델의 Generic Avatar와 원본 임포트 설정을 유지했다. 편집 상태의 Animator와 PlayerAnimationController는 활성이다. 구성은 [프로젝트 구조](PROJECT.md#astronaut-원본-리그-비교-구성)에 기록한다.
- PlayMode에서 프레임 진행을 확인했지만 정상 애니메이션 재생은 실패했다. Astronaut는 기존 코드의 Humanoid 검사에서 애니메이션 컴포넌트가 중지됐고 뼈 15개의 자세가 변하지 않았다. 기존 Player에서도 슬롯 이름 오류가 발생했다. 이번 요청은 원본 리그 비교이므로 코드·모델·공유 애니메이션 자산의 수정은 하지 않았다.
- 검사를 마치고 PlayMode를 종료했다. 이동·점프·실제 입력 조작과 빌드는 이번 검사 범위 밖이다.

## 기존 Player 구성: HFSM·SO·Animator 애니메이션 단순화

- 승인한 단순화 계획 (`docs/ai/plans/2026-10-11-player-animation-refactor.md`, 현재 파일 없음)에 따라 애니메이션 관련 런타임 스크립트를 SO와 연결 컴포넌트 두 개로 줄였다. HFSM 이벤트로 상태를 재생하고, Animator의 세 방향 Blend Tree가 이동 입력을 혼합한다.
- PlayerAnimation.controller의 8개 상태·17개 고유 슬롯 클립을 구성하고 기존 Player 프리팹/SO를 연결했다. 공통 0.15초를 상태 전환과 방향 완화에 사용한다. Landing·수동 Playables·개별 배율·보행 위상은 제거했다.
- Idle.fbx를 Humanoid/반복으로 임포트해 SO Idle에 등록했다. 나머지 16개 슬롯은 비어 있고 Idle로 대체된다. 현재 이동/공중/사망에서 다른 동작이 보이지 않는 것은 이 빈 슬롯 구성 때문이다.
- 독립 리뷰에서 확정 오류/회귀 없음. 최종 Unity 컴파일과 Editor 격리 검사 43개가 통과했다. 실제 완료 근거와 검사 한계는 단순화 계획 (`docs/ai/plans/2026-10-11-player-animation-refactor.md`, 현재 파일 없음)에 기록했다. 실제 조작·발 타이밍·PlayMode·빌드·네트워크는 검증 범위 밖이다.
- 다음 단계는 SO에 호환 Humanoid 클립을 등록하는 것이다. Idle/이동의 Loop Time을 켜고 Jump/Fall/Dead는 끈다. 실행 중 클립 참조를 교체했다면 재활성화한다. 구성과 역할은 [PROJECT.md](PROJECT.md)의 전신 애니메이션 절을 참고한다.
- 이전 애니메이션 계획 (`docs/ai/plans/2026-10-10-player-animation.md`, 현재 파일 없음)은 제거된 구현의 과거 기록이다. 그 검증 결과를 현재 Animator 구현의 검증으로 사용하지 않는다.
- 마무리 때 별도로 추가된 DoubleL 에셋 팩에서 .meta 파일 쓰기 오류가 발견됐다. 이번 애니메이션 검사 이후의 임포트 문제로 수정하지 않았으며 Console에 오류가 남아 있다. 상세 시점은 단순화 계획에 기록했다.

## 이전 작업: Player 입력·회전 분리와 임시 1인칭 카메라

사용자가 승인한 입력·회전 계획 (`docs/ai/plans/2026-10-10-player-input-look.md`, 현재 파일 없음)에 따라 PlayerScene 인스턴스의 Unity PlayerInput, PlayerInputReader, PlayerLook과 Cinemachine 카메라를 연결했다. Controller는 입력 적재 → 회전 → 기존 상태머신 Tick을 시작한다. 상세 구조는 [PROJECT.md](PROJECT.md)의 PlayerScene 절을 참고한다.

- 기존 이동 정책·수치와 사용자 재작성본/비교 파일을 보존했다. Settings 상속, HFSM root.Init(), 씬의 타입/설정 연결과 과거 Motor 참조를 최소 복구했다. 실행에서 발견한 기존 점프 차단은 Motor의 바닥 검사에 CharacterController 접촉 여유를 포함해 보완했다.
- 새 코드 학습 시 PlayerInputReader의 CurrentFrame/LookDelta → PlayerLook.Rotate → PlayerController.Update → 기존 Tick 흐름을 연결해서 설명한다. 예전 Controller의 입력 자산 복제·액션 수명 설명은 현재 구조에 적용하지 않는다.
- 독립 리뷰에서 재잠금 직후 새 점프 누락을 발견해 실제 버튼 홀드 스냅샷으로 수정했다. 수정본 재검토에서 추가 확정 오류는 없었다.
- 최종 Unity 컴파일 성공, PlayMode 임시 검사 40/40 통과, 신규 Console 오류 0. 입력 이벤트/포커스 콜백 시뮬레이션을 사용했으므로 실제 조작감·OS 창 전환은 수동 확인이 남는다. 세부 근거와 확인 절차는 위 계획에 있다. Play는 정지했고 임시 설정을 복구했다. 저장 후 Editor에 생긴 dirty 상태는 보존했다.
- 당시 남은 기능은 웅크림 시점 높이, 모델 애니메이션, 네트워크 소유권/동기화였다. 애니메이션 기반의 현재 상태는 위 절을 참고한다. 카메라는 고정 머리 높이와 자기 모델 표시를 유지한다.

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
| 5 | [PlayerMotor.cs](../../Assets/SJT/2_Scripts/Player/HFSM,MOVE/PlayerMotor.cs) | 명령을 받아 CharacterController 이동·중력·점프·충돌·캡슐 변경 | 상태 정책을 공부하기 전에 명령이 실제 몸에 어떤 영향을 주는지와 접지·수직 속도 등 결과를 이해한다. |
| 6 | [PlayerStateGraph.cs](../../Assets/SJT/2_Scripts/Player/HFSM,MOVE/PlayerStateGraph.cs) — 최초 안내명 `PlayerStates.cs` | HFSM 계층·상태 등록·전환 조건·상태별 이동 명령 | 앞의 입력·설정·명령과 몸 정보를 바탕으로 무엇을 결정하는지 이해한다. 가장 복잡한 단계이므로 작은 메서드 단위로 설명한다. |
| 7 | [PlayerStateMachine.cs](../../Assets/SJT/2_Scripts/Player/HFSM,MOVE/PlayerStateMachine.cs) | 그래프와 Motor 연결, 외부 API·상태 변경 이벤트 | 두 구성 요소의 역할을 안 뒤 명령 생성 → 실제 이동 → 이동 후 상태 갱신의 실행 순서를 배운다. |
| 8 | [PlayerController.cs](../../Assets/SJT/2_Scripts/Player/HFSM,MOVE/PlayerController.cs) | 입력 적재 요청·회전·컴포넌트 초기화·프레임 시작 | 마지막에 입력이 전체 흐름을 어떻게 시작하는지 연결한다. 처음부터 보면 입력·초기화·상태·물리를 동시에 따라가야 해서 어렵다. |

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
- 적용한 원본 버전: `1.1.6`
- 기본 경로·버전·직접 패키지·주요 폴더를 확인했다.
- 파일 배치·구조 검사와 실제 훅 신뢰·새 세션 스킬/역할 로딩은 구분한다.
- 게임 컴파일·실행은 이 적용 작업의 확인 대상이 아니다.
<!-- harness:status:end -->

## C# 리팩토링 공통 스킬

- csharp-refactor 스킬·한국어 UI·참고 문서 4개와 AGENTS 연결을 이 프로젝트와 공통 원본에 추가했다. 공통 원본 main의 v1.1.5, 커밋 c6d091b7421a01ff9bacb3d916fe792a92f268c8로 게시했다.
- 검증 결과·기존 HANDOFF 경로 오류 4개·PyYAML 부재·실제 자동 선택 미검증 및 설치 기준 유지 사항은 반영 계획 (`docs/ai/plans/2026-10-10-csharp-refactor.md`, 현재 파일 없음)에 기록했다. 게임 코드·자산과 다른 프로젝트는 변경하지 않았다.

## 로컬 디버그 가드 지침 동기화 (2026-10-11)

공통 원본의 디버그 가드 기준을 로컬 코딩 컨벤션·예시·Unity 보호 문서에 반영했다. 모든 개발용 로그·진단 전용 계산과 호출은 UNITY_EDITOR로 제외하고, 런타임 안전 검사·실패 처리는 유지한다. 게임 코드·자산은 이번 작업에서 변경하지 않았다.

## 델리게이트 공개 범위·명명 기준 (2026-10-11)

사용자 요청으로 델리게이트 멤버의 기본을 public event와 D_ + PascalCase로 정했다. 반드시 내부 전용이어야 할 때만 private/protected와 d_ + camelCase를 사용한다. 로컬 컨벤션 두 절과 예시를 맞췄으며 게임 코드·공통 원본은 변경하지 않았다.

공통 원본에도 동일한 기준을 병합했으며 UnityHarness main의 v1.1.6, 커밋 a728a1d3b076570895aadd2be0b1f1bc702c905f로 게시했다. 게시 파일은 VERSION·공통 코딩 컨벤션·예시 3개이며 다른 프로젝트는 자동 갱신하지 않았다.
