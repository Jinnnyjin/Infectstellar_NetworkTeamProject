# Infectstellar 게임 작업 인수인계

최종 갱신일: 2026-10-17 (Asia/Seoul). 경로는 Unity 프로젝트 루트 기준이다.

## 다음 작업: PlayerSyncRateController의 거리 기반 네트워크 LOD

- 2026-10-17 사용자와 합의한 방향: **거리 기반 네트워크 LOD를 구현한다. 송신 빈도를 조절하고, Interest Groups로 상대별 수신 빈도를 나눈다.** 사용자는 이를 `PlayerSyncRateController`의 내부 역할로 구성하고 씬의 `PlayerManager`에 컴포넌트로 붙일 예정이다. 이번 요청은 다른 채팅을 위한 인수인계 작성이며 구현·컴포넌트 부착은 아직 수행하지 않았다.
- 합의한 책임 분담: Controller가 각 클라이언트에서 로컬 플레이어 기준의 거리 판단, 송신 목표 빈도, 전송 그룹과 수신 그룹 구독을 조정하고, 기존 `_PlayerNetworkSync`가 결정에 따라 실제 스냅샷 작성·송수신을 담당한다. 현재 `Assets/SJT/4_Scenes/PlayerScene.unity`의 `PlayerManager`에는 `PlayerNetworkSyncManager`가 연결되어 있으며 `LocalPlayerSync`, 등록·해제 이벤트와 조회 기능을 제공한다. 목록 접근 방식은 구현 시 확인한다.
- 두 제어를 함께 사용한다. 가까운 상대가 있으면 높은 빈도로 보내면서 공통 그룹과 주변 구역 그룹에 업데이트를 나누어 먼 상대의 수신 빈도를 낮추고, 모두 멀면 내 스냅샷 작성·송신 자체도 줄이는 방향이다. 가까운 상대가 계속 모든 업데이트를 받도록 구독을 구성해야 한다. 공통 업데이트의 주기와 주변 업데이트 생략은 함께 설계하며, 송신률을 낮춘 뒤 기존 그룹 분배 비율만 유지해 먼 상대의 수신률까지 의도치 않게 낮추지 않는다.
- PUN2의 구현 수단: `OnPhotonSerializeView`에서 실제 작성 전 송신 차례를 판단하고 불필요한 스냅샷 생성·`SendNext`를 생략한다. 같은 PhotonView의 모든 관찰 대상이 데이터를 쓰지 않으면 해당 업데이트가 생략된다. `PhotonView.Group`으로 전송 그룹을 선택하고 `PhotonNetwork.SetInterestGroups`로 각 클라이언트의 수신 그룹을 관리한다. PUN이 거리별 Hz를 자동 계산하는 기능은 아니다.
- `PhotonNetwork.SendRate`와 `SerializationRate`는 **현재 클라이언트 전체**의 설정이며 다른 클라이언트로 자동 동기화되지 않는다. 수신 빈도 제한값도 아니다. 플레이어 스냅샷만 조절할 때는 기본 직렬화 주기를 유지하고 작성 여부를 선택하는 방법을 논의했다. 기본 주기 유지 여부를 포함한 구체적인 구현은 미확정이며, `SendRate`만으로 실제 패킷 빈도의 상한을 보장하지 않는다.
- 거리 임계값, 높은/낮은 Hz, 검사 간격, 경계 전환 완충, 그룹 번호·구역 분할·구독/해제 수명과 입퇴장 처리는 아직 미정이다. 대화의 30/10Hz·20/5Hz는 설명용 예시로 승인된 수치가 아니다. 공간 셀 기반 그룹은 정확한 플레이어 간 거리와 같지 않으므로 구역 설계도 필요하다. 실제 구현 요청 시 요구사항·작업 계획·검증 범위를 먼저 정한다.
- 재개 시 실제 파일을 다시 확인한다. 이번 기록 시점에는 `PlayerSyncRateController`가 없고 `_PlayerNetworkSync`는 송신 콜백마다 8항목을 작성한다. 앞선 설명에서 참고한 이전 `PlayerSyncRateManager.cs`와 `.meta`는 현재 사용자 작업으로 삭제된 상태이므로 복원하지 않는다. `PlayerNetworkRpc`도 PhotonView로 이벤트를 전송하므로 그룹 변경이 줍기·피해·손전등 등의 RPC 전달이나 생성·참조에 미치는 영향을 확인하고 스냅샷 최적화가 중요한 이벤트를 누락시키지 않도록 설계한다.
- 참고: [PUN Interest Groups 공식 문서](https://doc.photonengine.com/pun/current/gameplay/interestgroups), 동봉 `Assets/Photon/PhotonUnityNetworking/UtilityScripts/Culling/CullArea.cs`와 `CullingHandler.cs`. 네트워크 LOD는 설계 개념이고 Interest Groups는 구현 수단이다. 기존 Unity 자산·코드는 이번 인수인계에서 변경하지 않았으며 런타임·통신 성능은 미검증이다.

## 현재 상태: 플레이어 스냅샷 외삽

- 사용자 실행 승인에 따라 실제 PlayerMotor.Velocity를 8번째 항목으로 송수신하고, 기존 보간을 메서드로 분리했다. 재생 버퍼가 하나이면 XYZ 위치를 최대 0.1초 외삽하며 이후 예측 끝 위치를 유지한다. 회전·상태는 수신 값을 유지하고 예측 결과는 버퍼에 넣지 않는다.
- 참여 클라이언트는 같은 8항목 전송 형식을 사용해야 한다. 독립 리뷰에서 확정 오류는 없었으나, 이번 Unity 재컴파일은 시간 초과 후 triggered로 남아 완료를 확인하지 못했다. 계산 검증·Play·접속·빌드도 미검증이다. 최종 검증 근거와 재개 조건은 [외삽 계획](plans/2026-10-17-player-snapshot-extrapolation.md)에 기록한다.

## 현재 상태: 사망 직후 본인 삼인칭과 Tab 순환

- 로컬 사망 시 본인의 ThirdPersonView를 먼저 표시하고, 기존 Tab 순환에도 본인을 포함하도록 세 클래스의 사망 호출·제외 조건만 수정했다. 사용자 MineModel 토글·MineModel 프로퍼티·입력·부활·등록·네트워크·자산 변경은 보존한다.
- 중간 참가를 지원하지 않는 정책은 [프로젝트 전용 정책](PROJECT.md#이-프로젝트-전용-정책--공통-원본-반영-금지)에 기록했다. 이 정책과 이번 기록은 전역 하네스·공통 원본·다른 프로젝트에 반영하지 않는다.
- 독립 리뷰에서 새 회귀나 필수 추가 수정은 발견하지 못했다. 2026-10-17 15:56:38 KST 시작한 최종 Unity 재컴파일은 completed/failed:false/errors:[]/compilationFailed:false로 통과했고 세 소스의 검사 전후 SHA-256이 동일하다. 하네스 검사는 변경 전과 같은 기존 누락 참조 12건으로 실패했으며 Play·접속·빌드는 미검증이다. 상세 변경·검증·수동 확인은 [본인 관전 계획](plans/2026-10-17-self-third-person-spectate.md)을 따른다.

## 이전 기록: 사망 중 Tab 관전 전환 (2026-10-16)

- 로컬 생사/Tab 입력, 실제 ActorNumber 순환, 등록·비활성화·퇴장 이벤트와 카메라 전환을 연결했다. 원격 사망·신규 등록이 현재 관전 시점을 변경하지 않도록 했다. 사용자 Reader 초기화와 Health 변경은 유지하고 D_Revived 구독만 Controller에 추가했다.
- PlayerScene의 기존 카메라 두 개와 실제 네트워크 프리팹의 본인 카메라를 연결했다. 네트워크 Player.prefab에는 삼인칭 카메라가 없어 새 카메라 구성·연결이 필요하다. Health의 실제 체력 복구/부활 이벤트 발행도 현재 미구현이다.
- 독립 리뷰 추가 확정 오류 없음. 2026-10-16 20:21 KST Unity 최종 재컴파일은 오류 0건으로 통과했고 Editor에서 씬/프리팹 참조를 확인했다. 실제 Play·접속·빌드는 미검증이다. 문서 구조 검사는 기존 누락 참조 9건으로 실패했다. 상세 결과·남은 구성은 [관전 전환 계획](plans/2026-10-16-player-spectate.md)에 기록한다.

## 현재 상태: 10개 구역 메인 탐색 맵

- `Assets/SJT/3_Prefabs/MainGameMap/Sectors/`의 구역 프리팹 10개, `MainGameMap.prefab`, `Assets/SJT/4_Scenes/MainGameMapPreview.unity`를 완성했다. Sci-Fi Styled Modular Pack의 원본 모듈 스케일을 유지하며 단층 3,856㎡·방 30개·고정 소품 162개·조명 59개로 구성한다. 두 순환로와 04–10 샛길을 연결했다.
- 확인용 씬의 베이스캠프 인스턴스만 문짝을 열고 `(10,0,-5)`에 배치해 연결 기준점과 메인맵 입구를 `(10,0,0)`에 맞췄다. 기존 BaseCamp.prefab은 닫힘을 유지한다. 플레이어·줍는 아이템·적·스폰·게임 기능·런타임 생성기는 추가하지 않았다.
- 독립 리뷰 후 최종 정적 검증 `mainmap-static-20261015T050923539Z`를 통과했다. 바닥·천장 각 2,169점, 외벽 2,955점, 사람 크기 캡슐 경로 172개·겹침 326개에 실패 0. 241셀 전체 도달·30문/방 접근·13포털·저장11프리팹 참조·활성Animator0을 확인했다. 구역별 실내 시점과 전체 배치도 확인했다.
- 저장 과정에서 함께 바뀐 원본 머티리얼 13개는 시작 SHA256과 정확히 같은 바이트로 복원했다. 보호 대상 893개 파일이 전부 시작 시점과 일치한다. 새 컴파일 코드가 없고 맵의 기존 테스트가 없어 프로젝트 재컴파일·EditMode는 실행하지 않았으며, 실제 플레이 이동·멀티플레이·빌드·성능은 별도 확인이 필요하다. 문서 구조 검사는 기존 누락 계획 링크 4건으로 실패했다.
- 경로·접속 구조는 [프로젝트 설명](PROJECT.md#10개-구역-메인-탐색-맵), 상세 제작·최종 검증·한계는 [메인맵 계획](plans/2026-10-15-main-game-map.md)을 따른다. 임시 조립/검증 코드는 Temp/MainGameMap에만 있으며 영구 Unity 스크립트가 아니다.

## 현재 상태: PlayerNetworkRpc의 피해량 전달

- 사용자가 적용한 Health.D_DamageApplied/ApplyDamage를 재사용해 PlayerNetworkRpc에 Health 참조·구독/해제·피해 RPC를 추가했다. 최초 TakeDamage의 로컬 적용 이후 피해 대상 PhotonView로 Others에 float 피해량을 전송하고, 수신은 ApplyDamage만 호출해 재송신하지 않는다.
- 현재 ItemHit가 던진 사람 컴퓨터에서 판정하므로 피해 대상 소유권을 송신 조건으로 요구하지 않으며, 수신에서도 발신자와 피해 대상 ControllerActorNr의 일치를 요구하지 않는다. Health·JHJ 코드·Sync·씬·프리팹은 이번 작업에서 수정하지 않았다.
- 독립 리뷰에서 추가 범위의 확정 오류를 찾지 못했다. 2026-10-15 06:49 KST 최종 Unity 재컴파일은 completed/failed:false/compilationFailed:false/errors:[]로 통과했고 RPC와 Health의 SHA-256은 검사 전후 동일했다.
- 컴파일 중 기존 AstronautThirdPersonCamera의 미사용 필드 경고 2건과 기존 Pipeline 포트 파일 공유 위반 로그가 재발했다. 문서 구조 검사는 기존 누락 계획 링크 4건으로 실패했다. Play·네트워크 접속·빌드는 실행하지 않았으며 사망 연결·후발 입장 HP 복원은 남은 범위다. 구조는 [프로젝트 설명](PROJECT.md#플레이어-피해-rpc)을 따른다.

## 현재 상태: PlayerNetworkRpc의 최소 줍기 동기화

- 사용자 작성 Item.OnGrab → ApplyGrab(int) 연결을 재사용해 SJT의 PlayerNetworkRpc에 PlayerGrabber 참조·GrabRequested 구독/해제·줍기 RPC를 추가했다. 로컬에서 채워진 HeldItem의 ViewID를 Others로 보내고, 원격에서는 Player 제어 주체를 검증한 뒤 Item.ApplyGrab(발신자 번호)을 호출한다. 기존 Item.Grabbed 구독으로 원격 HeldItem이 연결된다.
- PlayerGrabber·Item·ItemNetwork와 씬·프리팹은 이번 작업에서 수정하지 않았다. 기존 손전등 RPC와 ItemNetwork의 Drop/Shoot/Stop 경로를 유지한다. 사용자 삭제 Manager/Spawner는 복원하지 않으며, 현재 PhotonPlayerSpawnerForTest와 Sync의 소유자 카메라 활성화 설정을 사용한다.
- 독립 리뷰에서 추가 범위의 확정 오류를 찾지 못했다. 2026-10-15 06:39:45 KST 최종 Unity 재컴파일은 completed/failed:false/compilationFailed:false/errors:[]로 통과했고 RPC 소스 SHA-256은 검사 전후 동일했다. 새 오류 0건, 기존 AstronautThirdPersonCamera의 미사용 필드 경고 2건이었다. 문서 구조 검사는 기존 인수인계의 누락된 계획 링크 4건으로 실패했다.
- 원격 HandsUp·동시 잡기 충돌 해결·후발 입장자의 소지품/손전등 복원은 남은 범위다. Play·접속·빌드는 실행하지 않았다. 역할과 연결은 [프로젝트 설명](PROJECT.md#jhj-아이템-동작과-플레이어-rpc-분리)을 따른다.

## 현재 상태: SJT 이전 RPC 잔여 코드 제거

- Health의 Sync 참조·피해 RPC 요청·원격 HP 적용·RPC 구독용 이벤트를 제거하고 로컬 피해/체력 계산을 유지했다. PlayerStateEvent·PlayerStateEventType·구형 PlayerSnapshot과 각각의 meta를 삭제했다.
- 사용자가 이미 삭제한 이전 Sync·Playback·PoseCalculator는 복원하지 않았다. 새 _PlayerNetworkSync는 전환 전 사용자 원본이며 이번에는 새 스냅샷 및 JHJ·로컬 손전등/HandsUp 코드를 수정하지 않았다.
- 사용자 지시에 따라 컴파일·테스트·검증은 실행하지 않았다. 상세 범위: [전환 계획](plans/2026-10-15-player-network-migration.md).

## 현재 상태: JHJ 플레이어 RPC 통합 롤백

- 사용자 요청으로 Item·PlayerGrabber의 이전 SJT 플레이어 RPC 통합 부분을 제거하고 JHJ 원래 동작/ItemNetwork 경로를 복원했다. 이후 JHJ의 피해 데이터 전달과 별도 HandsUp·생존 검사는 보존했다.
- ItemHit·ItemNetwork·ItemPhysics와 Unity 자산은 수정하지 않았다. 사용자 지시에 따라 컴파일/테스트/추가 검증은 실행하지 않았고 남은 Sync 오류는 자동 보완하지 않는다.
- 상세 범위와 다음 작업은 [전환 계획](plans/2026-10-15-player-network-migration.md)의 JHJ 롤백 절을 따른다.

## 현재 상태: 새 Sync의 추가 RPC 제거

- 사용자 요청으로 추가한 RPC와 _PlayerStateEventPlayback을 제거하고 Snapshot/Playback을 RPC 추가 전 상태로 복원했다. 새 관리자·스포너·프리팹·씬 연결과 스냅샷 송수신/보간 적용은 유지한다.
- Health.RequestDamage·Item.SendItemStop 호출은 남아 있으며 해당 API를 새로 만들기 전까지 컴파일 오류를 허용한다. 사용자 지시에 따라 검증과 자동 RPC 이식을 중단했다.
- 다음 작업은 사용자가 정한 순서로 RPC를 새로 만드는 것이다. 상세 상태: [전환 계획](plans/2026-10-15-player-network-migration.md).

## 현재 상태: 플레이어 공통 상태·자세 적용 메서드

- 사용자 작성 중인 메서드 이름 변경을 이어서 Motor의 TrySetCrouching(로컬 일어서기 검사)과 SetCrouching(확정 자세 적용)을 분리했다. StateMachine.SetState는 CurrentState·애니메이션·StateChanged를 갱신하며 HFSM 내부 전이를 실행하지 않는다. 기존 Playback의 남은 ApplyRemoteCrouch 호출도 SetCrouching으로 맞췄다.
- 로컬 이동은 TrySetCrouching을, 기존 원격 Playback과 새 _PlayerNetworkSync는 SetCrouching/SetState를 사용한다. 새 Sync의 올바른 공통 메서드 호출과 사용자 모델 처리·송수신 코드는 유지했다. 상세 역할은 [프로젝트 설명](PROJECT.md#플레이어의-로컬-판단과-공통-상태자세-적용)을 따른다.
- 독립 리뷰에서 이번 변경의 오류·회귀를 찾지 못했다. 2026-10-15 03:59 KST 최종 Unity 재컴파일은 completed/failed:false/errors:[]로 통과했다. 검사 전후 3개 변경 소스의 SHA-256이 동일했고 새 오류 0건, 변경 밖 AstronautThirdPersonCamera의 미사용 필드 경고 2건이었다. Play·접속·빌드·씬/프리팹 변경은 수행하지 않았다.
- 문서 구조 검사는 기존 HANDOFF의 누락된 계획 링크 4건으로 실패했다. 수정 전 기록에도 같은 링크가 있었고 해당 계획 파일이 없음을 대조했다. 이번 작업의 추가 참조에는 새 오류가 없으며, 과거 계획 기록은 변경하지 않았다.

## 현재 상태: 플레이어 아이템 RPC

- Grab·Drop·Shoot 및 대응 Stop을 기존 PlayerNetworkSync/PlayerSnapshotPlayback의 시간순 이벤트에 연결했다. 사용자 추가 Grab/참조/공개 Try 메서드를 완성했으며 손전등·HandsUp·기존 ItemNetwork API와 사용자 자산 변경은 보존했다.
- 로컬 Try는 즉시 실행 후 이벤트를 송신하고, 원격 Try 오버로드는 확정 아이템 ViewID·월드 자세·힘을 적용한다. 초기 소지 아이템 복원, 사망 시 Drop, 해당 해제 순서에 맞는 최종 Stop이 포함된다.
- 독립 리뷰의 발사 도중 입장 Stop 누락 경로를 수정하고 재검토했다. 최종 컴파일 결과는 검증 후 계획에 기록한다. Play/네트워크 접속/빌드는 실행하지 않는다.
- 수동 연결: 실제 Resources/PhotonPrefabs/Player의 PlayerGrabber.releasePoint가 비어 있다. socket은 기존 HeadSocket을 유지한다. TryShoot는 새 키 바인딩 없이 호출 API로 제공한다. 씬 배치 Player (1)은 기존 PUN 생성 초기화 계약을 만족하지 않는다. 두 명의 동시 잡기 승인 정책과 이미 정지한 월드 아이템 전체의 초기 동기화는 별도 범위다.
- 상세 구현 순서·검증·다음 확인은 [아이템 RPC 계획](plans/2026-10-14-player-item-rpc.md)을 따른다.

## 현재 상태: 플레이어 네트워크 파일 통합

- 사용자 요청에 따라 6개 내부 클래스와 .meta를 정리하고 Playback(버퍼·시계·이벤트), NetworkSync(전송 예약), SyncRateManager(등록·거리 등급)에 private 메서드·필드로 통합했다. 기존 컴포넌트 4개, 순수 보간/외삽 계산, 공통 상수 파일만 유지한다. 한국어 주석·기능별 region·Inspector 설정·공개 API/RPC·전송 형식·기존 GUID를 유지했다.
- 독립 리뷰에서 확정된 회귀 없음. 00:32:49 KST 최종 소스 추출 코어 6그룹·12,810 conditions/1,500프레임 기존 구현 비교 통과. 00:32:58 KST 전체 재컴파일은 동일 SDK 오류 24종으로 실패했다. 실제 컴포넌트 연결/Inspector/접속/Play/빌드는 미검증이다. 범위·근거·다음 확인은 [파일 통합 계획](plans/2026-10-14-player-network-consolidation.md)에 둔다.

## 이전 상태: 플레이어 네트워크 책임 분리·학습용 주석

- 승인한 계획대로 Network 폴더에 버퍼/시계/보간·외삽/이벤트/전송 일정/등록/거리 등급의 internal 클래스 7개를 추가했다. 기존 4개 MonoBehaviour와 Snapshot/Event를 정리하고 변수·매개변수·복잡한 단계의 한국어 주석과 Inspector Header를 추가했다. 기존 API·전송 순서·설정·씬/프리팹/기존 GUID와 사용자 변경은 유지했다.
- 독립 리뷰에서 확정된 회귀를 찾지 못했다. 23:51:24 KST 실제 Unity/PUN 참조의 임시 코어 검사 6그룹·12,810 conditions와 1,500프레임 기존/분리 결과 비교가 통과했다. Registry와 네 컴포넌트의 실제 연결은 이 검사에 포함하지 않는다.
- 23:50:04 KST 전체 Unity 재컴파일은 기존 SDK 참조 오류 24종으로 실패했으며 추가 진단 없음. 실제 의존 DLL 부재로 Registry/MonoBehaviour까지의 제한 컴파일도 불가했다. Play/접속/빌드/Inspector 로딩은 미검증이다. 상세 근거와 다음 확인은 [책임 분리 계획](plans/2026-10-13-player-network-responsibilities.md)에 둔다.

## 현재 상태: 플레이어 네트워크 정책값 Inspector 노출

- 네트워크 관련 5개 기존 스크립트를 수정하고 PlayerNetworkConstants.cs/.meta를 추가했다. 조절값 16개를 SerializeField로 노출하고 기존 기본값·스냅샷/RPC 형식·기존 씬/프리팹/GUID/참조를 유지했다. 기존 HANDOFF의 사용자 변경도 보존했다.
- 독립 최종 리뷰·하네스 구조 검사와 보호 파일 해시 확인을 완료했다. 22:57:56 KST 실제 Unity 재컴파일은 변경 전과 같은 외부 SDK 타입 미정의 24건으로 실패했으며 새 진단은 없었다. Constants/Snapshot/기존 PlayerState만 실제 Unity/PUN DLL로 제한 컴파일 성공; MonoBehaviour 4개의 전체 컴파일·새 필드 Inspector 로딩·접속 동작은 미검증이다.
- 조절 위치·적용 시점과 남은 검증은 [정책값 계획](plans/2026-10-13-player-network-settings.md)을 따른다. 기존 외부 의존성을 사용자가 복구한 환경에서 전체 재컴파일 및 네트워크 Player 프리팹/씬의 Inspector 기본값을 확인한다. 이번 작업에서는 설치·Play·빌드·커밋·푸시를 수행하지 않았다.

## 현재 상태: PlayerSyncRateManager 씬 싱글톤

- Manager의 Instance 등록·중복 비활성화·소유자만 정리/참조 해제를 추가하고, NetworkSync는 관리자 탐색 대신 Initialize(PlayerSyncRateManager.Instance)를 사용한다. 두 기존 스크립트만 변경했으며 거리별 전송·적응형 보간·스포너 Inspector·씬·프리팹은 유지한다.
- 사용자 학습 파일의 추가/삭제 작업을 보존한다. 이전 _PlayerSnapshotPlayback 미정의 오류는 과거 검사 결과이며 현재 상태로 단정하지 않는다. 이번 최종 검증과 재개 조건은 씬 싱글톤 계획 (`docs/ai/plans/2026-10-13-player-manager-singleton.md`, 현재 파일 없음)을 따른다.
- 최종 전체 Unity 컴파일(20:00:48 KST)과 독립 리뷰, 격리 수명 검사 4개 시나리오/18 assertions를 통과했다. 20:07:06 KST에 임시 객체·싱글톤/PUN 전역값을 복원하고 PlayerScene dirty=false/roots6/stopped를 확인했다. Play/MPP/빌드는 수행하지 않았다.

## 이전 상태: PUN2 코드 가독성 리팩토링

- 사용자 선택대로 거리별 전송·적응형 보간을 유지하고 NetworkSync/Playback/Manager/Snapshot 네 파일의 책임별 메서드와 계산 표현을 정리했다. 새 스크립트·컴포넌트·자산은 없으며 라이트 지연이나 실제 송신 빈도를 변경한 작업은 아니다.
- 사용자 학습 파일 `_PlayerSnapshotPlayback.cs`와 설정 변경·기존 주석을 보존했다. 실제 Unity 컴파일은 해당 학습 파일의 ProcessEvents 미정의(CS0103)로 실패했으며 이번 변경 4파일의 오류는 보고되지 않았다. 아래 과거 MPP 결과를 새 수정본의 전체 컴파일·Play 통과로 사용하지 않는다.
- 독립 소스 리뷰, 전송 계약과 보호 파일 해시 확인을 완료했다. 기준/최종 Playback을 별도로 동적 컴파일하고 격리 프리팹 비교 7개 시나리오·30,461 assertions와 기대값 검사 9건을 통과했다. 기존 도메인 의존의 제한 검증이며 새 수정본의 MPP/Play는 실행하지 않았다. 상세 결과와 남은 전체 컴파일 확인은 네트워크 리팩토링 계획 (`docs/ai/plans/2026-10-13-player-network-refactor.md`, 현재 파일 없음)을 따른다.

## 이전 상태: PUN2 플레이어 동기화 구현

- Resources/PhotonPrefabs/Player에 PlayerNetworkSync/PlayerSnapshotPlayback/PhotonView를 연결했다. 로컬 제어만 입력·카메라·HFSM/이동을 수행하며 원격은 RPC/스냅샷을 Photon 시간축에서 재생한다. 초기 전체 상태, 순서/생사 버전, 30/15/10Hz 거리별 전송과 고정/적응형 버퍼 비교를 구현했다.
- PlayerScene은 관리자 하나와 기존 로그인 씬 인스턴스의 스포너를 사용한다. 사용자 선택으로 단독 Player를 추가하지 않고 PlayerSpawnPoints를 스폰 중심으로 사용한다. 방 입장 전에는 플레이어가 없다. 기존 로그인/SJT Player 원본은 보존했다.
- 독립 리뷰와 격리 Editor 상태 로직 7개 시나리오/1,822개 assertion을 통과했다. 최종 컴파일/자산 참조 검사 결과, 추가·수정 파일과 실제 MPP 실행 검증은 PUN2 동기화 계획 (`docs/ai/plans/2026-10-13-player-pun2-sync.md`, 현재 파일 없음)에 둔다. 추가 실행 검증 결과는 아래를 따르며 충돌·점프·커서 삭제 관찰·지연/손실·Profiler 비교·빌드는 남아 있다.
- 초기 구현 당시 미저장 Editor 씬은 보호했다. 추가 실행 검증에서는 최신 저장된 씬을 사용하고 종료 시 미저장 없음도 확인했다. IDamageable.cs와 사용자 학습 파일의 동시 변경을 보존한다. 게임 저장소 커밋/푸시는 하지 않았다.

사용자 추가 실행 요청으로 MPP 메인+추가2의 실제 검증을 수행했다. 추가 Editor의 옛 메모리 씬(manager/spawner0)을 미저장 없음 확인 후 최신 PlayerScene 재로드로 해결했다. 3명/고유 슬롯/로컬만 입력·카메라, W/Ctrl/마우스 동기화, API 손전등·HandsUp·생사, 재입장 초기 상태, 전송 등급/버퍼 옵션을 확인했다. 15Hz 실측14.96/10Hz10.00이나 30Hz 설정은23.30Hz라 목표 미달 원인 조사가 남는다. 최종 Play 정지·씬미저장없음·기본 거리/옵션 복구, 세 클라이언트 새 Console 오류0을 확인했다. 실행 중 원본 Playback 변경은 필드 주석32개 추가뿐이며 제거한 파일의 SHA가 시작본과 같고 최신 컴파일도 통과했다. 상세 근거/추가 미검증은 위 계획에 기록한다.

## 현재 상태: 손전등 제어와 R키 연결

- FlashLightSetter를 FlashlightController로 이름 변경하고 기존 GUID를 보존했다. 최초 꺼짐, R키 신규 입력 반전, Inspector의 두 표면 색, 사망 즉시 끄기·입력 차단, 부활 꺼짐 유지, 일반 재활성화 상태 유지와 변경 이벤트를 구현했다. Light.enabled와 모델의 헬멧 슬롯 2에 MaterialPropertyBlock을 사용한다.
- 독립 리뷰·최종 Unity 컴파일·타입 반영 확인·하네스 구조 검사를 통과했다. 새 컴파일 오류 0건이며 기존 카메라 스크립트 경고 2건이다. 프리팹·씬·Material·Input Actions는 수정하지 않았다. Player 루트에 컴포넌트를 추가하고 ViewPivot/Flashlight의 Light, Player_TPose_Unrigged의 SkinnedMeshRenderer, 슬롯 2와 두 색을 직접 연결해야 한다. Inspector 연결·Play는 미검증이며 상세 결과는 손전등 제어 계획 (`docs/ai/plans/2026-10-12-flashlight-controller.md`, 현재 파일 없음)에 둔다.

## 현재 상태: HandsUp 호출 API 추가

- PlayerAnimationController에 `SetHandsUp(true/false)`를 추가했다. Arms 레이어의 적용 Weight를 기존 BlendDuration으로 전환하고, 켜질 때만 HandsUp을 처음부터 재생한다. 활성화/비활성화 때 적용은 꺼진 상태로 초기화한다. 이동 Base Layer와 독립적이며 입력·사망·네트워크 호출은 별도 연결이 필요하다.
- 사용자 구성 Animator·마스크·클립·씬·프리팹은 수정하지 않았다. 독립 코드 검토와 하네스 구조 검사는 통과했다. 전체 Unity 컴파일은 병행 작성 중인 `Assets/SJT/2_Scripts/Player/Input/FlashLightSetter.cs`의 Color 타입 충돌(CS0104, 6·7행)과 Action<> 미정의(CS0246, 11행)로 실패했으며 해당 파일은 수정하지 않았다. 최종 HandsUp의 실행 사례와 실제 이동 중 팔 자세·전환 느낌은 미검증이다.

## 이전 작업: PlayerScene 기본 손전등

- Cookie 없는 흰색 기본 Spot Light와 약한 환경광·어두운 카메라 배경을 씬에 적용했다. 광원의 로컬 Z 0.2 이동으로 카메라 앞 Player 표면의 과한 근접 반사를 피한다. Player 프리팹·입력·모델·기존 사용자 변경은 보존했다.
- 독립 리뷰와 최종 저장본 Play 검증 36개 assertions를 통과했다. 기존 Player 모델의 화면 하단 가림, 10~15m 벽/물체 식별과 Mobile/성능은 남은 확인이다. 단계·완료 근거·로그/병행 변경의 한계는 손전등 계획 (`docs/ai/plans/2026-10-12-player-flashlight.md`, 현재 파일 없음)에 둔다.

## 현재 상태: Animator 직접 관리 전환

- PlayerAnimationController의 런타임 Override 생성·Controller 복원과 클립 대체를 제거하고 Animator에 연결된 Controller를 그대로 사용한다. 상태별 해시·HFSM 직접 호출·동일 상태 재시작 방지·0.15초 완화를 유지한다.
- 공유 PlayerController.controller의 8개 상태/3개 방향 Blend Tree에 기존 실제 클립 17개를 직접 연결했다. Settings에는 전환 시간만 남는다. 이후 클립 변경은 Animator에서 한다. 기존 GUID·프리팹/씬/백업·실제 클립 파일을 보존했다.
- 독립 리뷰·최종 Unity 컴파일·직접 Motion 연결 검사 170개 조건을 통과했다. 새 오류 0건이며 대상 밖 기존 경고 6건이다. 단계·결과·Play 미검증 항목은 Animator 직접 관리 계획 (`docs/ai/plans/2026-10-12-player-animator-direct.md`, 현재 파일 없음)을 따른다. 기존 Walk 시작 부분 반복 증상의 해결은 아직 확인하지 않았다.

## 현재 상태: 웅크리기 카메라 높이 연동 완료

- Motor가 실제 캡슐 자세 변경 후 Look에 줄어든 높이를 전달하고, Look이 기존 ViewPivot을 DOTween으로 양방향 0.2초 이동한다. HeadLookPoint 연결과 기존 사용자 캡슐/씬 Override·비율 0.65를 유지한다. Controller와 백업은 변경하지 않았다.
- 독립 검토·최종 Unity 컴파일·읽기 전용 프리팹/씬 참조 검증을 통과했다. 새 컴파일 오류 0, 기존 파일 경고 6이다. 실제 Play의 전환 느낌·머리 자세는 사용자가 확인한다. 상세 범위·결과·수동 확인은 카메라 높이 계획 (`docs/ai/plans/2026-10-12-player-crouch-camera.md`, 현재 파일 없음)을 따른다.

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

## 로컬 모듈러 맵 제작 스킬 (2026-10-15)

- [assemble-modular-map](../../.agents/skills/assemble-modular-map/SKILL.md)을 추가했다. 샘플 씬 기준 분석, Unity Search 스킬 연결, 모듈 규격·접합 확인, 시험 조립·전체 배치·시각/구조 검증을 안내한다. AGENTS의 작업별 참고 표와 한국어 UI를 연결했다.
- 이 프로젝트의 로컬 하네스에만 추가했으며 실제 에셋 팩·샘플 씬 경로와 맵 구조는 제작 요청 시 확인한다. 게임 코드·자산·공통 원본은 변경하지 않았다. 자동 선택과 실제 Editor 맵 제작은 아직 검증하지 않았다.
- 독립 정적 시나리오 검토에서 확정 문제를 찾지 못했다. 새 스킬과 연결 파일의 제한된 메타데이터/UI 형식·로컬 링크/앵커 검사는 통과했다. skill-creator의 quick_validate.py는 PyYAML 부재로 실행 불가이며 전체 하네스 검사는 기존 누락 계획 링크 4건이 남아 있다.

## 베이스캠프 제작 (2026-10-15)

- 사용자가 승인한 8×8·높이 4.4·scale 1의 빈 방을 Modular_SciFi_Pack Metal로 조립했다. 결과는 `Assets/SJT/3_Prefabs/BaseCamp/BaseCamp.prefab`과 `Assets/SJT/4_Scenes/BaseCampPreview.unity`다. 기존 MapMakingScene과 원본 팩은 보존했다.
- 정면 +Z의 Door4와 상부 창·환기구·발광 벽·천장 패널·문 옆 패널을 배치했다. 원본 뒤판을 보정한 문틀 및 양면 문짝 메시만 SJT에 파생했다. 메인맵은 연결하지 않았고 출입문 주변 외벽까지만 마감했다.
- 세부 구성·수정 후 검증·남은 범위는 [베이스캠프 계획](plans/2026-10-15-basecamp.md)을 따른다. 문은 닫힌 상태로 저장했으며 개폐·판매·네트워크·스폰 기능은 없다. PlayMode·실제 이동·빌드는 수행하지 않는다.

## C# 리팩토링 공통 스킬

- csharp-refactor 스킬·한국어 UI·참고 문서 4개와 AGENTS 연결을 이 프로젝트와 공통 원본에 추가했다. 공통 원본 main의 v1.1.5, 커밋 c6d091b7421a01ff9bacb3d916fe792a92f268c8로 게시했다.
- 검증 결과·기존 HANDOFF 경로 오류 4개·PyYAML 부재·실제 자동 선택 미검증 및 설치 기준 유지 사항은 반영 계획 (`docs/ai/plans/2026-10-10-csharp-refactor.md`, 현재 파일 없음)에 기록했다. 게임 코드·자산과 다른 프로젝트는 변경하지 않았다.

## 로컬 디버그 가드 지침 동기화 (2026-10-11)

공통 원본의 디버그 가드 기준을 로컬 코딩 컨벤션·예시·Unity 보호 문서에 반영했다. 모든 개발용 로그·진단 전용 계산과 호출은 UNITY_EDITOR로 제외하고, 런타임 안전 검사·실패 처리는 유지한다. 게임 코드·자산은 이번 작업에서 변경하지 않았다.

## 델리게이트 공개 범위·명명 기준 (2026-10-11)

사용자 요청으로 델리게이트 멤버의 기본을 public event와 D_ + PascalCase로 정했다. 반드시 내부 전용이어야 할 때만 private/protected와 d_ + camelCase를 사용한다. 로컬 컨벤션 두 절과 예시를 맞췄으며 게임 코드·공통 원본은 변경하지 않았다.

공통 원본에도 동일한 기준을 병합했으며 UnityHarness main의 v1.1.6, 커밋 a728a1d3b076570895aadd2be0b1f1bc702c905f로 게시했다. 게시 파일은 VERSION·공통 코딩 컨벤션·예시 3개이며 다른 프로젝트는 자동 갱신하지 않았다.

## 모듈러 맵 스킬 공통 원본 게시 (2026-10-15)

- 로컬 assemble-modular-map 스킬을 공통 원본에 병합했다. SJT 배치 문구를 대상 프로젝트 규칙으로 일반화했고, AGENTS 연결·한국어 UI·README 스킬 수 9개·VERSION 1.1.7을 맞췄다. UnityHarness main의 커밋 622e6915eddc0e7a0d81d5cbeaf5144fc37387f9로 게시했다.
- 공통 템플릿 구조·새 스킬의 제한된 형식/UI·상대 참조/앵커·변경 보호 검사와 독립 정적 검토를 통과했다. 로컬 전체 검사는 기존 HANDOFF 누락 계획 링크 4건으로 실패했고 quick_validate.py는 PyYAML 부재로 실행하지 못했다. 실제 자동 선택·Editor 조립·시각/이동 동작은 미검증이다. 게임 코드·자산·설치 기준과 다른 프로젝트는 이번 작업에서 변경하지 않았다.
