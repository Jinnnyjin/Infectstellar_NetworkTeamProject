# 🎮 Infectstellar

> 친구들과 돈을 벌어 탈출하세요. 단, 당신 중 한 명은 감염되어 있을 수 있습니다.

> 소개영상 [YouTube](아직없음)
---

## 1. 프로젝트 개요

| 항목 | 내용 |
| --- | --- |
| **게임명** | Infectstellar |
| **장르** | 1인칭 협동 생존 · 소셜 디덕션 (리썰컴퍼니 + 어몽어스) |
| **한 줄 정의** | 우주 기지에서 아이템을 모아 팔아 목표 골드를 벌고 탈출하는 게임. 단, 게임 도중 한 명이 무작위로 감염자가 된다 |
| **동시 접속 인원** | 최대 4인 (Photon PUN 2) |
| **플랫폼** | PC (키보드 & 마우스) |
| **링크** | [팀 Notion 보드](https://app.notion.com/p/3ea8b10bccf680ca8c86f8c85bf5bc0c) \| [GitHub Repository](https://github.com/Jinnnyjin/Infectstellar_NetworkTeamProject) |

### 팀원별 담당 영역

| 이름 | 담당 |
| --- | --- |
| **진희진** (JHJ) | 아이템 · 상호작용 |
| **김현준** (KHJ) | 로비 · 게임 진행 |
| **심지태** (SJT) | 플레이어 · 몬스터 |

---

## 2. 실행 방법

### 개발 환경

| 항목 | 내용 |
| --- | --- |
| **Unity** | `6000.3.6f1` (버전 불일치 시 셰이더/네임스페이스 오류 발생) |
| **Render Pipeline** | URP |
| **네트워크** | Photon PUN 2 |
| **백엔드** | Firebase (Auth 익명 로그인 / Realtime Database) |

### 조작 키

| 키 | 동작 |
| --- | --- |
| `W A S D` | 이동 |
| `Mouse` | 시점 회전 |
| `Shift` | 달리기 |
| `Ctrl` | 앉기 |
| `Space` | 점프 |
| `E` | 아이템 줍기 / 내려놓기 / 상호작용 (문, 상점, 탈출 버튼) |
| `좌클릭` | 들고 있는 아이템 던지기 |
| `R` | 손전등 On/Off |
| `Tab` | (사망 시) 관전 대상 변경 |
| `Esc` | 커서 해제 |

---

## 3. 핵심 플레이 흐름

```
타이틀(익명 로그인 · 닉네임) → 로비(방 목록 · 방 생성/입장 · 채팅) → 대기방(준비)
  → 로딩 → 게임(아이템 수집 · 판매 · 감염) → 탈출 / 전멸 → 정산 · 랭킹 등록 → 타이틀(재대전)
```

---

## 4. 주요 기능 목록

| 분류 | 기능 |
| --- | --- |
| **로비** | Firebase 익명 로그인, 닉네임 저장/불러오기, 방 생성 · 랜덤 입장 · 방 목록, 대기방 플레이어 패널(색상), 채팅 |
| **게임 진행** | 게임 상태 FSM, 전원 준비 확인 후 시작, 공유 골드, 시간 경과 후 무작위 감염자 선정, 탈출 / 전멸 / 감염자 승리 판정, 랭킹 등록 |
| **플레이어** | HFSM 기반 이동(걷기/달리기/앉기/점프), 1인칭 카메라(Cinemachine), 손전등, 체력 · 피격 · 사망 · 부활, 관전 모드 |
| **몬스터** | 몬스터 AI(상태 기반), 스폰 포인트 기반 스폰, 출입 제한 구역, 스냅샷 기반 위치 동기화 |
| **아이템** | 희귀도별 가중치 스폰, 줍기 / 내려놓기 / 던지기(물리), 아이템 피격, 소비 아이템(부활 주사기), 판매 구역 |
| **상점 · 기지** | 상점 구매, 베이스캠프 문 개폐, 탈출 버튼 |
| **UI** | HUD(플레이어 상태 슬롯, 들고 있는 아이템, 상호작용 안내, 키 프롬프트), 베이스캠프 정보, 결과창 |

---

## 5. 프로젝트 구조

```
Infectstellar/Assets
├── KHJ/                 # 김현준 - 로비 · 게임 진행 · Firebase
│   ├── Scripts/
│   │   ├── Firebase/    # FirebaseLinkManager
│   │   ├── Photon/      # PhotonLinkManager, PhotonRoomListHandler
│   │   ├── Game/        # GameManager, GameStateManager, PlayerDataObserver
│   │   ├── Loading/     # LoadingHelper
│   │   └── UI/          # Menu, Room, Chat, ErrorPanel ...
│   └── Scenes/          # TitleScene, LoadingScene, LeaveRoom, EmptyScene
├── SJT/                 # 심지태 - 플레이어 · 몬스터 · 맵
│   ├── 2_Scripts/
│   │   ├── Player/      # HFSM 이동, Input, Network, Animation
│   │   ├── Monster/     # MonsterController, MonsterNetworkSync, Spawner
│   │   ├── Network/     # SnapshotBuffer
│   │   ├── Manager/     # 카메라, 동기화 주기 관리
│   │   └── Entity/      # Health, DamageSource
│   ├── 4_Scenes/        # PlayerScene, MainGameMapPreview, BaseCampPreview
│   └── 7_Docs/          # 몬스터 / 맵 최적화 문서
├── JHJ/                 # 진희진 - 아이템 · 상호작용 · 상점 · HUD
│   ├── Scripts/
│   │   ├── Item/        # Item, ItemNetwork, ItemPhysics, ItemSpawner ...
│   │   ├── Player/      # PlayerGrabber, PlayerInteractor
│   │   ├── Shop/        # Shop, ShopProduct
│   │   ├── Button/      # DoorToggleButton, EscapeButton
│   │   └── UI/          # HUDController, InteractInfoView, ResultView ...
│   ├── Editor/          # 아이템 콜라이더 자동 맞춤, 아이콘 캡처 툴
│   └── Datas/           # ItemData / SpawnTable / Shop SO
└── Resources/PhotonPrefabs/   # PhotonNetwork.Instantiate 대상 프리팹
```

### 주요 Scene

| Scene | 역할 |
| --- | --- |
| `TitleScene` | 로그인 · 닉네임 · 로비 · 대기방 |
| `LoadingScene` | 게임 씬 로딩 |
| `MainGameScene` | 메인 게임 맵 |
| `LeaveRoom` | 게임 종료 후 방 나가기 처리 → 타이틀 복귀 |


## ⚠️ 유료 에셋 및 저작권 안내 (Paid Assets Notice)
- 본 프로젝트는 유료 에셋을 포함하고 있습니다.
- 무단 복제 및 상업적 재배포를 금지하며, 내부 협업 목적으로만 공유됩니다.
- 모든 유료에셋은 `.gitignore` 로 제외되었습니다.


