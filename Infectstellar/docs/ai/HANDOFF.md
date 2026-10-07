# Infectstellar 게임 작업 인수인계

최종 갱신일: 2026-10-07 (Asia/Seoul). 경로는 Unity 프로젝트 루트 기준이다.

## 현재 구현 상태

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

독립 리뷰의 활성화 시 Sprint/Crouch 홀드 누락과 천장 검사 여유 문제를 보완했고 최종 재검토에서 추가 확정 오류는 없었다. 2026-10-07 17:54:44 KST 최종 Unity 컴파일은 완료·성공했으며 새 오류 0건이었다. 씬/설정/스크립트 참조도 확인했다. 검증 근거와 한계는 [기본 이동 작업 계획](plans/2026-10-07-player-movement.md)의 최신 기록을 따른다. 씬 연결은 저장했으며 이후 다시 표시된 Editor의 미저장 상태는 임의 저장·리로드하지 않고 보호했다.

PlayMode·게임 실행·빌드는 수행하지 않는다. 다음으로 계획의 수동 확인 목록에 따라 입력·점프·천장·낙하·착지·사망·부활·재활성화를 확인한다. 이후 카메라/애니메이션/네트워크는 요구사항을 정한 뒤 연결한다.

2026-10-06의 상태 관리 전용 구현과 과거 5,470 assertion 결과는 [이전 HFSM 계획](plans/2026-10-06-player-hfsm.md)에 남아 있다. 해당 결과를 이번 이동 코드의 런타임 검증으로 사용하지 않는다.

<!-- harness:status:start -->
## 하네스 적용 상태
- 적용한 원본 버전: `1.1.1`
- 기본 경로·버전·직접 패키지·주요 폴더를 확인했다.
- 파일 배치·구조 검사와 실제 훅 신뢰·새 세션 스킬/역할 로딩은 구분한다.
- 게임 컴파일·실행은 이 적용 작업의 확인 대상이 아니다.
<!-- harness:status:end -->
