using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>로그인 코드를 유지하면서 방 입장 후 네 개 슬롯 중 하나에 자기 플레이어를 생성합니다.</summary>
[DisallowMultipleComponent]
public sealed class PhotonPlayerSpawnerForTest : MonoBehaviourPunCallbacks
{
    private const string c_prefabPath = "PhotonPrefabs/Player"; // PUN 기본 풀에서 읽을 Resources 내부 경로
    private const int c_spawnGridWidth = 2; // 현재 네 슬롯을 배치하는 한 행의 칸 수
    private const float c_minWaitInterval = 0.001f; // 대기·확인 간격의 양수 하한(초)
    [Header("생성 연결·시작 위치")]
    [SerializeField] private PlayerSyncRateManager manager; // 등록·시작 슬롯을 관리하는 씬 연결점
    [SerializeField] private Transform spawnOrigin; // 네 시작 위치의 기준 Transform
    [SerializeField] private Vector3 originOffset; // 기준 위치에 더할 월드 이동량(m)
    [SerializeField, Min(0f)] private float slotSpacing = 1.5f; // 기준에서 각 슬롯을 떨어뜨릴 축별 거리(m)
    [Header("생성 대기")]
    [SerializeField, Min(c_minWaitInterval), Tooltip("앞선 플레이어의 생성 등록을 기다릴 최대 시간(초)")]
    private float spawnWaitTimeout = 10f; // 앞선 Actor의 등록을 기다릴 최대 시간(초)
    [SerializeField, Min(c_minWaitInterval), Tooltip("앞선 플레이어의 등록 여부를 다시 확인하는 간격(초)")]
    private float spawnCheckInterval = 0.1f; // 등록 완료 여부를 다시 확인할 간격(초)

    private GameObject localPlayer; // 내 클라이언트가 생성한 네트워크 플레이어
    private Coroutine spawnRoutine; // 현재 진행 중인 생성 대기 코루틴
    private bool hasSpawned; // 이 방에서 생성 또는 내 등록을 이미 확인했는지 여부

    /// <summary>스폰 대기 시간과 확인 간격이 양수가 되도록 Inspector 설정을 보정합니다.</summary>
    private void OnValidate()
    {
        spawnWaitTimeout = Mathf.Max(c_minWaitInterval, spawnWaitTimeout);
        spawnCheckInterval = Mathf.Max(c_minWaitInterval, spawnCheckInterval);
    }

    /// <summary>이미 입장한 상태에서 씬을 시작해도 같은 생성 처리를 예약합니다.</summary>
    private void Start() => BeginSpawn();

    /// <summary>방 입장을 확인한 뒤 생성 처리를 예약합니다.</summary>
    public override void OnJoinedRoom() => BeginSpawn();

    /// <summary>퇴장하면 생성 대기와 보관 참조를 정리합니다.</summary>
    public override void OnLeftRoom() => ClearSpawn();

    /// <summary>연결이 끊기면 자동 재접속 없이 생성 대기와 보관 참조를 정리합니다.</summary>
    public override void OnDisconnected(
        DisconnectCause cause) // PUN이 전달한 연결 종료 원인
    {
        ClearSpawn();
    }

    /// <summary>콜백 등록 해제와 함께 비활성화된 스폰 작업도 중단합니다.</summary>
    public override void OnDisable()
    {
        base.OnDisable();
        CancelSpawnWait();
    }

    /// <summary>같은 방에서 중복 콜백이나 Start가 발생해도 한 번만 생성합니다.</summary>
    private void BeginSpawn()
    {
        if (!isActiveAndEnabled || !PhotonNetwork.InRoom || hasSpawned || localPlayer != null || spawnRoutine != null)
        {
            return;
        }
        if (manager == null || !manager.isActiveAndEnabled || spawnOrigin == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PhotonPlayerSpawnerForTest] 관리 객체와 스폰 기준 위치를 연결해야 합니다.", this);
#endif
            return;
        }
        spawnRoutine = StartCoroutine(SpawnWhenEarlierActorsAreReady());
    }

    /// <summary>앞선 ActorNumber의 생성 등록을 설정된 시간까지 기다린 뒤 빈 슬롯을 선택합니다.</summary>
    private IEnumerator SpawnWhenEarlierActorsAreReady()
    {
        // 코루틴 핸들을 먼저 보관하게 하고 PUN의 입장/생성 이벤트 처리를 한 프레임 기다린다.
        yield return null;
        // 시간과 확인 간격은 대기 시작 때 확정한다. 진행 중 Inspector 변경은 다음 대기에 적용된다.
        float deadline = Time.realtimeSinceStartup + spawnWaitTimeout; // 이번 대기를 끝낼 실제 경과 시각(초)
        var wait = new WaitForSecondsRealtime(spawnCheckInterval); // 이번 코루틴에서 재사용할 확인 간격
        while (PhotonNetwork.InRoom && !hasSpawned)
        {
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber; // 내 생성 순서를 구분할 방 안 번호
            if (manager.HasActor(actorNumber))
            {
                hasSpawned = true;
                break;
            }
            if (AreEarlierActorsReady(actorNumber))
            {
                hasSpawned = TrySpawnLocalPlayer();
                break;
            }
            if (Time.realtimeSinceStartup >= deadline)
            {
#if UNITY_EDITOR
                Debug.LogError($"[PhotonPlayerSpawnerForTest] 앞선 플레이어의 생성 등록을 {spawnWaitTimeout}초 안에 확인하지 못했습니다.", this);
#endif
                break;
            }
            yield return wait;
        }
        spawnRoutine = null;
    }

    /// <summary>먼저 입장한 Actor의 생성 등록이 모두 처리됐는지 확인합니다.</summary>
    private bool AreEarlierActorsReady(int actorNumber) // 이 번호보다 작은 Actor를 기다릴 기준
    {
        foreach (Player player in PhotonNetwork.PlayerList) // 내 생성 전에 등록되어야 할 방 참가자
        {
            if (player.ActorNumber < actorNumber && !manager.HasActor(player.ActorNumber))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>빈 슬롯을 선택하고 내 네트워크 플레이어를 한 번 생성합니다.</summary>
    private bool TrySpawnLocalPlayer()
    {
        int slot = manager.FindFreeSlot(); // 현재 모든 복제본이 알고 있는 첫 빈 시작 위치 번호
        if (slot < 0)
        {
#if UNITY_EDITOR
            Debug.LogError("[PhotonPlayerSpawnerForTest] 빈 시작 슬롯이 없어 생성하지 않습니다.", this);
#endif
            return false;
        }
        Vector3 position = CalculateSpawnPosition(slot); // 선택한 슬롯의 월드 위치(m)
        localPlayer = PhotonNetwork.Instantiate(c_prefabPath, position, spawnOrigin.rotation, 0, new object[] { slot });
        return localPlayer != null;
    }

    /// <summary>현재 네 슬롯의 2×2 배치를 기준 위치 주변의 월드 좌표로 계산합니다.</summary>
    private Vector3 CalculateSpawnPosition(int slot) // 좌우·앞뒤를 선택할 슬롯 번호(0~3)
    {
        float horizontalSign = slot % c_spawnGridWidth == 0 ? -1f : 1f; // 짝수 칸은 왼쪽, 홀수 칸은 오른쪽
        float forwardSign = slot < c_spawnGridWidth ? -1f : 1f; // 첫 행은 뒤쪽, 둘째 행은 앞쪽
        Vector3 offset = new Vector3(horizontalSign * slotSpacing, 0f, forwardSign * slotSpacing); // 기준 중심 주변의 슬롯 이동량(m)
        return spawnOrigin.position + originOffset + offset;
    }

    /// <summary>비활성화·퇴장 시 진행 중인 생성 대기를 해제합니다.</summary>
    private void CancelSpawnWait()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    /// <summary>다음 명시적 방 입장을 위한 로컬 생성 상태를 비웁니다.</summary>
    private void ClearSpawn()
    {
        CancelSpawnWait();
        if (localPlayer != null)
        {
            PlayerNetworkSync sync = localPlayer.GetComponent<PlayerNetworkSync>(); // 퇴장한 이전 객체의 로컬 입력을 중단할 연결점
            if (sync != null)
            {
                sync.SuspendLocalControl();
            }
        }
        localPlayer = null;
        hasSpawned = false;
    }
}
