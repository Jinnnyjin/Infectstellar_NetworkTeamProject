# 공통 몬스터 사용 안내

## 현재 구성

- [Monster 프리팹](../3_Prefabs/Monster.prefab)은 Spider 모델을 연결한 공통 몬스터다. [MapTestScene](../4_Scenes/MapTestScene.unity)의 Spider 한 개가 Supply 진입 통로 `(20,0,14.14)`, Y 회전 90도로 배치되어 있다.
- [MonsterController](../2_Scripts/Monster/MonsterController.cs)가 UnityHFSM 평면 FSM의 배회·추적·공격·수색·경직·사망을 처리한다. 기존 Health·DamageSource를 사용한다.
- PhotonView와 [MonsterNetworkSync](../2_Scripts/Monster/MonsterNetworkSync.cs)가 Master의 AI·이동·공격 판정과 원격 재생을 구분한다. 첫 데이터는 [SpiderSettings](../5_Data/SpiderSettings.asset)다. 애니메이션·스폰·풀링은 없다.

## 몬스터 종류를 바꾸는 세 단계

1. 프리팹의 `Model` 아래 모델을 교체한다. 발은 로컬 Y=0 부근, 몸통은 루트 중심, 정면은 루트 +Z에 맞춘다. 모델 내부 스케일·회전으로 맞추고 루트 scale은 `(1,1,1)`을 유지한다. 원본 모델 프리팹에 변경을 일괄 Apply하지 않는다.
2. Project의 `Assets/SJT/5_Data`에서 `Create > Monster > Settings`로 SO를 만들고, 루트 `MonsterController.Settings`에 지정한다. 체력·공격력·속도·감지 거리·시간을 조절한다. 실행 전 Inspector에서 설정하며 실행 중 SO 교체는 지원하지 않는다.
3. `AttackArea`의 BoxCollider `Center`와 `Size`를 조절한다. 필요하면 자식 Transform 위치도 조절한다. Collider는 비활성 상태를 유지한다. 범위는 별도 사거리 수치 없이 공격 진입과 타격 판정에 함께 사용된다.

모델 변경 후 AI 참조를 다시 연결하지 않는다. 공통 몸체는 CapsuleCollider 반지름 0.35m·높이 0.8m·중심 Y=0.4m이며 NavMeshAgent도 같은 치수다. 펼친 다리는 별도 충돌·피격 판정에 포함하지 않는다. 몸체보다 큰 모델의 다리는 벽·문틀과 겹칠 수 있다.

첫 공격 박스는 Center `(0,0.8,0.9)`, Size `(1.2,1.6,1.2)`다. 공격 중 루트 이동·회전은 멈추고, SO 준비 시간이 지난 순간 박스 안의 생존 Player 모두에게 한 번씩 피해를 준다. 타격 시 벽 검사는 없으며 콜라이더가 여러 개인 대상도 한 번만 맞는다.

## 이동과 금지 구역

- MainGameMap 인스턴스의 NavMeshSurface는 PhysicsColliders·Children·Default 레이어, Monster Agent Type을 사용한다. [베이크 데이터](../5_Data/Navigation/MainGameMap_Monster.asset)는 정적 맵용이며 런타임에 다시 굽지 않는다.
- Agent Type `Monster`는 반지름 0.35m·높이 0.8m·단차 0.2m다. 기존 Humanoid 설정은 유지한다.
- `MainGameMap/MonsterRestrictedZone`은 NavMeshModifierVolume의 같은 Center·Size를 베이크 차단과 타깃·공격 제외에서 사용한다. 월드 중심 `(2,2,12.07)`, 크기 `(24,8,32.14)`로 BaseCamp·Arrival의 세 방과 연결 복도를 덮는다.
- 안전구역 판정은 Player 루트 위치 기준이다. 몸 일부가 밖으로 나와도 루트가 안이면 감지·피해 대상에서 제외한다. 진행 중 공격은 유지하지만 안쪽 Player는 맞지 않는다.
- 구역은 몬스터 초기화 때 자동으로 찾아 캐싱한다. 몬스터마다 구역을 연결하지 않는다. 런타임 구역 생성·교체는 이번 범위에 없다.
- 맵 또는 금지 범위를 편집하면 기존 NavMeshSurface에서 다시 Bake한다. 문은 열린 상태의 통로이며 개폐 AI는 없다. NavMesh 경계에 닿아도 배회·추적·수색 경로는 금지 구역을 통과하지 않는다.

## 네트워크 동작과 Play 확인

게임 시작 전에 참가자를 같은 Photon 룸에 모은 뒤 MapTestScene을 실행한다. 몬스터는 게임 씬에서 한 번 초기화하며 OnJoinedRoom·중도 입장 복구를 추가하지 않는다. 이 씬을 바로 Play해 접속했다면 참가자가 모인 뒤 씬에 재진입해야 몬스터가 시작한다. 기존 접속·Player 스포너는 수정하지 않았다.

- 씬 Spider의 PhotonView는 고유 sceneViewId=2, Fixed 소유권, Unreliable 전송이며 MonsterNetworkSync만 관찰한다. 프리팹의 sceneViewId=0은 정상이며 씬 배치 시 고유 ID가 필요하다. 현재 Spider는 연결되어 있다.
- Master만 NavMeshAgent·감지·FSM·공격 박스를 실행한다. 다른 클라이언트는 [MonsterSnapshot](../2_Scripts/Monster/MonsterSnapshot.cs)의 위치·Y 회전·속도·행동·종료 시각을 [MonsterSnapshotPlayback](../2_Scripts/Monster/MonsterSnapshotPlayback.cs)으로 재생한다. 보간 지연은 기본 0.1초, 외삽은 최대 0.1초다.
- 공격자가 몬스터 복제본에 피해를 즉시 적용하고 Others RPC로 피해량을 보낸다. 수신 피해는 재송신하지 않으며 경직은 Master가 결정한다. 이동 스냅샷은 HP를 덮어쓰지 않는다. 몬스터의 Player 타격은 기존 PlayerNetworkRpc 경로로 전파한다.
- Master 교체 시 최신 수신 원본 위치에서 AI를 시작하고 체력·경직/사망 잔여 시간을 유지한다. 이동 기억은 버리고 주변을 다시 감지한다. 공격 중이면 미실행 타격을 폐기하고 SO의 AttackRecovery를 기다린다. 이전 송신자의 버퍼를 비운다.
- 사망 즉시 몸체 충돌을 중단하고, Master가 남은 CorpseDuration 후 PhotonNetwork.Destroy로 제거한다. 룸 이탈·연결 종료 시 씬에 남은 몬스터는 멈춘다. MonsterNetworkSync가 없는 기존 구성은 단독 AI로 동작한다.
- PlayerPlayback과 몬스터 Playback은 [SnapshotBuffer](../2_Scripts/Network/SnapshotBuffer.cs)의 32개 시간순 버퍼를 공유한다. Player의 전송 형식은 유지한다.

모든 감지·타격 대상은 Player 레이어와 활성 Health를 갖는다. 새 대상은 10m, 기존 타깃은 20m까지 수평 시야각 120도와 벽 가림으로 확인한다. 설정은 SO에서 변경한다. 경직은 마지막 피격부터 0.5초, 사망 시 충돌 중단 후 3초 뒤 제거가 기본값이다.

기본 행동의 기존 검증은 [Spider 작업 기록](../../../docs/ai/plans/2026-10-18-spider-monster.md), 네트워크 구현·두 클라이언트 검증은 [네트워크 작업 기록](../../../docs/ai/plans/2026-10-18-monster-network.md)을 따른다.
