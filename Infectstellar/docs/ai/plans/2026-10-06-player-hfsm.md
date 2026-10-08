# 플레이어 HFSM 상태 관리
상태: 완료 | 작성·갱신일: 2026-10-06

## 목표와 완료 조건

승인된 계획에 따라 UnityHFSM 2.3.0으로 플레이어 상태 관리 코드를 구현한다. Alive 아래 Grounded(Idle/Walk/Run/CrouchIdle/CrouchWalk), Airborne(Jump/Fall)을 두고 Root의 Dead로 즉시 사망한다. 외부 상황 전달 한 번에 최종 상태를 결정하며 실제 변경만 한 번 알린다. 게임 중 부활은 이전 상황을 초기화한 Idle로 시작한다.

## 범위와 근거 파일

- PlayerStateMachine 컴포넌트, 한 파일의 PlayerStates 그래프·전환 규칙, PlayerState enum을 추가한다.
- 입력·이동·물리·카메라·Photon·집기·던지기·휘두르기·감정표현·체력·스태미나는 제외한다. 씬·프리팹·입력 자산을 변경하지 않는다.
- manifest/lock와 설치 package.json: UnityHFSM 2.3.0, Git f8fdc2efb862522acd26fab6cd940886c84cde37. 기존 플레이어 코드·프리팹·테스트·asmdef와 PUN2 SDK는 확인되지 않았다.
- 개인 컨벤션을 적용하되 사용자가 요청한 상태 코드 한 파일 구성을 우선한다. HFSM의 AddState/AddTransitionFromAny, 중첩 StateMachine, forceInstantly 트리거, OnLogic의 부모→활성 자식 실행, OnExit의 재귀 종료를 실제 설치 코드에서 확인했다.

## 단계와 진행 상태

1. 요구사항·환경 조사: 완료. 외부 상황 자동 판단, 공중 웅크리기 착지 반영, 게임 중 Idle 부활 확정.
2. 코드 작성: 완료.
3. 독립 검토·최종 컴파일·가능한 상태 전환 검증: 완료. 아래 실행 범위와 미검증 항목을 구분한다.
4. PROJECT/HANDOFF·개인 CONTEXT 갱신, 하네스 구조 검사·최종 변경 확인: 완료.

## 주요 결정과 이유

- 외부 API: UpdateMovementContext(isMoving, wantsToRun, wantsToCrouch, isGrounded, verticalVelocity), Die, Revive, CurrentState, IsAlive, StateChanged(previousState, currentState).
- 시간·입력 폴링 없이 상황 전달 시 Root.OnLogic를 호출한다. 공중에서 상승 속도 > 0이면 Jump, 0 이하이면 Fall이다. 지상에서는 웅크리기가 달리기보다 우선하며 달리기 의도만으로 정지 상태가 Run이 되지 않는다.
- 중첩 계층의 내부 초기 상태를 외부에 알리지 않고 전체 전환 완료 후 컴포넌트가 최종 상태 변경만 발행한다. 초기화 알림은 없다.
- 그래프는 한 번 만들고 비활성화로 초기화하지 않는다. 다른 컴포넌트의 Awake 등에서 먼저 호출해도 중복 생성하지 않는다. 사망 중 이동 상황은 보관하지 않고 무시하며 명시적인 부활만 허용한다.
- 상태별 실행 동작이 아직 없으므로 불필요한 빈 파생 클래스를 만들지 않고 라이브러리가 제공하는 가벼운 StateBase 등록을 활용한다. 이후 상태 동작도 PlayerStates.cs에 작성한다.
- 신규 테스트 인프라·PlayMode·빌드·설치·패키지 변경은 하지 않는다.

## 검증 결과

- 수정 전 editor_status: 현재 프로젝트, Unity 6000.3.6f1, ready, playMode=stopped. 준비 상태 조회는 컴파일 검증이 아니다.
- 수정 전 Console cursor=3/session=e048dbff5f35430694647c23a07ce762: Collections 테스트 DLL 경로 누락 예외와 Pipeline 통신 오류가 남아 있으며 compilationFailed=false. 이번 코드 이전 오류로 분리한다.
- unity_reviewer 독립 리뷰: 승인 계획과 직접 관련된 확정 오류·회귀 위험 없음. 부모→새 하위 계층 전환, 배타적 지상 조건, 사망·부활, 최종 이벤트 중복 방지, 비활성화 시 유지·파괴 시 종료를 실제 라이브러리와 대조했다. 리뷰 중 발견한 PROJECT 중복 제목은 제거했고 이후 코드 변경은 없다.
- unity_validator 최종 컴파일: 2026-10-06 20:28:50~20:29:30 KST에 recompile 실행 후 recompile_status가 completed, failed=false, errors=[], compilationFailed=false를 반환했다. 실행 ID/시작 시각은 도구에서 제공하지 않아 별도 ID를 만들지 않았다.
- 실제 Assembly-CSharp의 내부 PlayerStates를 Editor의 순수 관리 코드 eval/reflection으로 검사했다(20:30:50.152~20:30:50.434 KST). 생존 7개 상태 × bool 16조합 × 수직 속도 -1/0/+1의 336개 전환 조합, 사망 중 336개 상황 무시와 필드 보존, 사망·부활·반복 호출·Idle 초기화·다음 상황 반영, 실제 중첩 계층 및 Exit 재귀 종료를 확인했다. 5,470개 assertion, 실패 0개. 라이브러리 StateChanged 카운터로 같은 상태 재진입이 없는 것도 확인했다. 신규 테스트 파일·어셈블리·인프라는 만들지 않았다.
- 새 코드의 MonoScript 임포트와 Editor가 생성한 .meta를 확인했다. 스크립트 GUID: PlayerStateMachine=991a1caf6e1d5f74da6ee5cae5ab32f6, PlayerStates=22024f28cb8e5e04f924673acfa1aac5, PlayerState=00223d71b8539384d8aa881fa693b88f. enum MonoScript의 GetClass는 null이지만 실제 enum은 컴파일·상태 검증에서 사용됐다.
- 최종 검사 전후 SHA-256 동일: PlayerStateMachine 3C095B7CC2A0B8E3EC8294A8D028FE302A9FAF3B7D179AEE233015823D36B293, PlayerStates D777C22668BC2F11C9C85B6DBD189456A439D549F05A282787CBF9FED8BC9D1F, PlayerState C1A3AAE7E33DA45304B75E7AE8BE487B9766AFB124FBB045EA74E21879160C12.
- 최초 씬 상태 조회 eval이 Pipeline Main thread operation timed out after 5000ms로 한 번 실패하며 기존 cursor 3 이후 seq 4 로그를 추가했다. 후속 eval은 정상 완료했고 console(since:4)는 새 로그 0개, reset=false, dropped=false였다. 기존 Collections DLL·Pipeline 문제 해결은 범위 밖이며 새 플레이어 컴파일 오류는 없었다. 마지막 Editor 상태는 ready/stopped이고 씬 조회에서 미저장 변경 없음.
- 하네스 구조 검사 통과: AGENTS 113/250줄, 스킬 6개, 로컬 참조 129개. 내용·연결과 필수 메타데이터 검사이며 Unity 동작 검증과 구분한다. git diff --check 및 작업 파일 공백·충돌 표식, Scripts 하위 신규 GUID 중복 검사도 통과했다.
- 부모 훅 토큰과 MCP 자동 영수증 지원이 없어 승계용 영수증 ID는 없다. 실제 MCP 완료 결과·시각·최종 코드 해시를 근거로 기록하며 임의 통과 영수증을 만들지 않았다.

## 남은 문제와 다음 행동

- MonoBehaviour의 공개 StateChanged 이벤트 발행·구독 해제·활성화/비활성화 수명은 독립 코드 검토만 수행했으며 GameObject를 만드는 실행 검사는 하지 않았다. 관련 기존 EditMode 테스트는 없고 PlayMode·게임 실행·빌드는 수행하지 않았다.
- 플레이어 객체에 PlayerStateMachine을 부착하고 외부 이동 시스템에서 최신 상황을 전달하는 연결은 후속 작업이다. 이번 구현은 상태 관리 코드 완료이며 실제 플레이어 이동·애니메이션·물리·Photon 플레이 검증 완료를 뜻하지 않는다.
- 공개 이벤트는 구독자의 수명에 맞춰 등록·해제한다. 파괴 시 컴포넌트가 보관한 구독 참조를 해제한다. 감정표현 이동 제한과 네트워크 권한·동기화 정책은 후속 구현 때 확정한다.
