# 맵 프리팹 최적화 정리

작성일: 2026-10-18 · 대상: MainGameMap / BaseCamp · Unity 6000.3.6f1 / URP 17.3.0

맵에서 반복 사용하는 원본 프리팹의 그림자·충돌체·배칭·오클루전 설정을 개선했다. 아래는 전체 프리팹 목록 대신 **대표 원본의 경로 + 외 N개 + 분류 기준**으로 정리한 적용 범위다.

경로는 Unity 프로젝트 루트 기준이다. **‘외 N개’는 대표를 제외한 원본 프리팹 종류 수**이며 맵 배치 수가 아니다. LOD·복도 내부 모델 때문에 한 프리팹에 Renderer가 여러 개일 수 있다. 각 최적화 대상은 서로 겹치므로 기법별 수량을 합산하지 않는다.

## 적용 현황

| 최적화 기법 | 적용 범위 | 현재 상태 |
| --- | --- | --- |
| 작은 소품의 그림자 생성 줄이기 | 원본 6종 / Renderer 7개 | Cast Shadows Off 적용 |
| 충돌체 단순화 | 원본 2종 | MeshCollider → 바닥 Box 1개 / 캐비닛 Box 6개 |
| Static Batching 준비 | 원본 44종 / Renderer 소유 객체 61개 | Batching Static 적용 |
| 오클루전의 가려지는 대상 지정 | 원본 47종 / Renderer 소유 객체 64개 | Occludee Static 적용 |
| 오클루전의 가리는 역할 지정 | 위 47종 중 18종의 객체 28개 | Occluder Static도 추가 |

**설정 반영은 확인했지만 성능 개선량은 측정하지 않았다.** 배칭 설정만으로 모든 물체가 한 번의 드로우콜로 묶이는 것은 아니며, 실제 배칭 여부는 재질 등 조건에 따라 달라진다.

## 1. 작은 소품의 그림자 생성 줄이기

작은 장식·의자·작은 테이블·천장 조명 외형을 기준으로 분류해 **Mesh Renderer의 Cast Shadows만 Off**로 변경했다.

| 분류 | 대표 프리팹과 수량 | 적용 내용 |
| --- | --- | --- |
| 작은 장식 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Decorative elements/decorative_plant_small.prefab` 외 0개 | 작은 화분의 그림자 생성 끄기 |
| 고정 의자 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Decorative elements/decorative_chair.prefab` 외 1개 | 일반 의자와 조종석 의자의 그림자 생성 끄기. 일반 의자는 LOD0·LOD1 모두 적용 |
| 작은 테이블 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Decorative elements/Tables/decorative_table_small_glass.prefab` 외 1개 | 유리형·일반형 작은 테이블의 그림자 생성 끄기 |
| 천장 조명 외형 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Lights/light_celing_1.prefab` 외 0개 | 조명 기구 모델의 그림자 생성 끄기. Light의 밝기·범위는 유지 |

- **Receive Shadows는 유지**했다. 빛이 없는 곳에서 계속 밝게 보이도록 만드는 설정이 아니며 다른 물체의 그림자도 받을 수 있다.
- 큰 가구·기계·벽·바닥·천장·문은 이 그림자 변경에서 제외했다.
- 해당 물체가 그림자 있는 광원의 영향을 받을 때 그림자 계산 비용 감소를 기대하는 변경이다. MainGameMap의 기존 Point Light 59개는 이미 그림자가 꺼져 있어, 그 광원들의 그림자 비용을 추가로 줄였다고 보지 않는다.

## 2. MeshCollider를 단순한 BoxCollider로 교체

**프리팹당 BoxCollider 최대 6개로 필요한 빈 공간을 유지할 수 있는 단순한 형태**를 기준으로 교체했다.

| 분류 | 대표 프리팹과 수량 | 적용 내용 |
| --- | --- | --- |
| BaseCamp의 평평한 방 바닥 | `Assets/Import/Modular_SciFi_Pack (146 Objects, LowPoly)/Prefabs/Metal/Floor/Floor4_4.2.prefab` 외 0개 | MeshCollider 1개를 BoxCollider 1개로 교체. 외곽 4×0.2×4m 유지, 깊이 최대 약 2.1cm의 장식 홈 평탄화 |
| 판으로 구성된 캐비닛 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Decorative elements/cabinet.prefab` 외 0개 | MeshCollider 1개를 좌우·뒤·아래·위·중간판 BoxCollider 6개로 교체. 두 수납 공간 유지 |

- 선반은 빈 공간을 보존하려면 BoxCollider 6개를 초과하므로 기존 MeshCollider를 유지했다. 의자·문턱·문·계단·다른 설비도 교체하지 않았다.
- 캐비닛 상판 돌출부 아래 약 6.2cm 구간은 단순화됐다. 모델 외형·배치는 유지했다.
- 충돌 Layer·물리 재질·Trigger 설정을 유지하고 Rigidbody를 추가하지 않았다.
- 기존 물리 비용이 작았으므로 큰 성능 개선을 보장하지 않는다. 실제 이동·아이템 배치·레이캐스트 확인은 별도로 필요하다.

## 3. Batching Static 설정

**움직이지 않는 불투명 구조물·가구·소품·설비**를 기준으로 분류해 Renderer가 달린 객체에 **Batching Static**을 추가했다.

| 분류 | 대표 프리팹과 수량 | 적용 내용 |
| --- | --- | --- |
| 고정 구조물과 부속 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Walls/decorative_wall_6.prefab` 외 21개 | 두 맵의 벽·바닥·천장·복도와 BaseCamp의 고정 부속까지 원본 22종에 Batching Static 적용 |
| 고정 가구·소품·설비·조명 외형 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Decorative elements/cabinet.prefab` 외 21개 | 가구·장식 16종, 기계 5종, 조명 기구 1종의 원본 22종에 Batching Static 적용 |

- 합계 **원본 44종 / Renderer 소유 객체 61개**다. 복도 내부 모델과 각 LOD 모델도 포함했다. Renderer 없는 루트까지 일괄 체크하지 않았다.
- 문 프리팹 2종은 고정 문틀까지 제외했다. 유리 벽·큰 유리 테이블·작은 유리 테이블의 투명 원본 3종도 제외했다.
- 빌드 시 정적 배칭 대상으로 표시한 것이다. 프로젝트의 Static Batching 옵션은 이미 켜져 있었으며 렌더링 방식·FBX Read/Write·런타임 코드는 바꾸지 않았다.
- 실제 드로우콜·메모리 변화는 아직 측정하지 않았다.

## 4. 오클루전 컬링용 Static 설정

두 역할을 구분했다. **Occludee는 가려지면 그리기를 생략할 대상**, **Occluder는 뒤의 물체를 가리는 역할**이다. 하나의 객체에 두 역할을 함께 설정할 수 있다.

### 가려지는 대상 — Occludee Static

| 분류 | 대표 프리팹과 수량 | 적용 내용 |
| --- | --- | --- |
| 두 맵의 고정 Renderer 전체 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Decorative elements/cabinet.prefab` 외 46개 | 고정 구조물·가구·소품·설비·조명 외형·유리를 기준으로 원본 47종의 객체 64개에 Occludee Static 적용 |

Batching Static 대상 44종에 유리 원본 3종을 추가한 범위다. **유리는 뒤를 가리는 물체로 쓰지 않지만, 벽 뒤에 숨었을 때 그리기를 생략할 대상으로는 포함**했다. 움직이는 문이 포함된 프리팹 2종은 전체 제외했다.

### 뒤를 가리는 역할도 추가 — Occluder Static

| 분류 | 대표 프리팹과 수량 | 적용 내용 |
| --- | --- | --- |
| 불투명한 일반 벽 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Walls/decorative_wall_6.prefab` 외 3개 | 큰 차폐면이 있는 벽 원본 4종, LOD를 포함한 객체 6개에 Occluder 추가 |
| 복도 내부 벽·불투명 바닥 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Corridors/Corridor_1.prefab` 외 1개 | 복도 원본 2종의 내부 벽과 floor_4, 객체 10개에만 Occluder 추가 |
| 속이 막힌 컨테이너 | `Assets/Sci-Fi Styled Modular Pack/Prefabs/Machines/container_big.prefab` 외 1개 | 불투명한 닫힌 상자 형상의 큰/작은 컨테이너 2종, 객체 2개에 Occluder 추가 |
| BaseCamp 큰 구조 면 | `Assets/Import/Modular_SciFi_Pack (146 Objects, LowPoly)/Prefabs/Metal/Floor/Floor4_4.2.prefab` 외 9개 | 큰 벽 8종·방 바닥 1종·방 천장 1종의 원본 10종, 객체 10개에 Occluder 추가 |

합계 **원본 18종에 포함된 객체 28개**다. 이 객체들은 Occludee에도 포함되며 별도 28개를 더 만든 것이 아니다.

다음은 **Occludee만** 설정했다.

- 격자 구멍을 AlphaClip 재질로 표현하는 floor_2·floor_5와 복도 내부 floor_2. 복도의 floor_4는 불투명하므로 위 Occluder 대상에 포함했다.
- 열린 가구·의자·책상·침대·소파·식물·유리와 작은 소품.
- 형상이나 차폐 효율이 불확실한 배터리·발전기·로터, 좁은 조명 바·장식 부품·문턱.

### Bake 및 Scene 뷰 확인 상태

- 프리팹 설정 작업에서는 Bake하지 않았다. **이후 사용자가 진행한 단계에서 베이크 데이터가 생성된 상태를 확인**했다.
- 현재 `Assets/SJT/4_Scenes/MapTestScene.unity`에는 `Assets/SJT/4_Scenes/MapTestScene/OcclusionCullingData.asset` 참조가 저장돼 있다. 이 사실만으로 모든 방의 컬링 결과가 올바르다고 확정하지 않는다.
- 직전 읽기 전용 확인 시 베이크 데이터 약 50KB가 로드됐고 Main Camera의 Occlusion Culling은 켜져 있었다. 다만 카메라 선택이 없고 Occlusion Culling 창이 표시되지 않아 Scene 뷰의 컬링 미리보기가 꺼진 상태였다. 이는 그 확인 시점의 상태이며 현재 UI가 같다고 보장하지 않는다.
- 미리보기는 **Occlusion Culling 창과 Scene 뷰를 함께 표시 → Visualization 탭 → 실제 Camera 선택** 순서로 확인한다. 카메라를 베이크 영역 안에 두고, 벽 뒤 물체가 생략되는지와 문·창 너머 필요한 물체가 남는지를 확인해야 한다.
- Bake 결과는 Occlusion Culling 창의 Bake 탭에서 **Clear로 제거한 뒤 조정·재베이크**할 수 있다. 프리팹의 Static 체크와 베이크 데이터는 별개다.

## 보존한 설정과 남은 확인

- 각 작업의 지정 항목 외 모델·배치·재질·조명·LOD·GUID와 관련 없는 인스턴스 Override는 유지했다. 그림자·충돌체 변경 후 추가한 Static 설정에서도 앞선 변경을 보존했다.
- 원본 프리팹을 공유하는 다른 사용처에도 변경이 반영된다. 인스턴스가 해당 값을 Override했거나 원본 연결 없이 복사된 객체라면 별도 확인 대상이다.
- BaseCamp 원본이 있는 `Assets/Import`는 기존 Git 제외 대상이다. 파일은 로컬에서 수정됐지만 일반 Git 변경 목록에는 포함되지 않으므로 팀 공유 시 별도 확인이 필요하다. 추적 정책은 변경하지 않았다.
- 저장값·참조·diff와 독립 검토로 설정 반영을 확인했다. **최적화 전후 FPS·프레임 시간·드로우콜·메모리 비교는 아직 하지 않았다.** 실제 배칭, 컬링 정확성, LOD 전환, 이동·아이템 충돌은 후속 확인 대상이다.

## 공식 참고

- [Unity 6.3: 빌드 시 배칭 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/DrawCallBatching-Enable.html)
- [Unity 6.3: 오클루전 컬링 대상 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/occlusion-culling-getting-started.html)
- [Unity 6.3: Bake·Clear·Visualization 사용법](https://docs.unity3d.com/6000.3/Documentation/Manual/occlusion-culling-window.html)
