using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>로그인 코드를 유지하면서 방 입장 후 네 개 슬롯 중 하나에 자기 플레이어를 생성합니다.</summary>
[DisallowMultipleComponent]
public sealed class PhotonPlayerSpawnerForTest : MonoBehaviourPunCallbacks
{
    private const string c_prefabPath = "PhotonPrefabs/Player";
    [SerializeField] private PlayerSyncRateManager manager;
    [SerializeField] private Transform spawnOrigin;
    [SerializeField] private Vector3 originOffset;
    [SerializeField, Min(0f)] private float slotSpacing = 1.5f;

    private GameObject localPlayer;
    private Coroutine spawnRoutine;
    private bool hasSpawned;

    /// <summary>이미 입장한 상태에서 씬을 시작해도 같은 생성 처리를 예약합니다.</summary>
    private void Start() => BeginSpawn();

    /// <summary>방 입장을 확인한 뒤 생성 처리를 예약합니다.</summary>
    public override void OnJoinedRoom() => BeginSpawn();

    /// <summary>퇴장하면 생성 대기와 보관 참조를 정리합니다.</summary>
    public override void OnLeftRoom() => ClearSpawn();

    /// <summary>연결이 끊기면 자동 재접속 없이 생성 대기와 보관 참조를 정리합니다.</summary>
    public override void OnDisconnected(DisconnectCause cause) => ClearSpawn();

    /// <summary>콜백 등록 해제와 함께 비활성화된 스폰 작업도 중단합니다.</summary>
    public override void OnDisable()
    {
        base.OnDisable();
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
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

    /// <summary>앞선 ActorNumber의 생성 등록을 최대 10초 기다린 뒤 빈 슬롯을 선택합니다.</summary>
    private IEnumerator SpawnWhenEarlierActorsAreReady()
    {
        // 코루틴 핸들을 먼저 보관하게 하고 PUN의 입장/생성 이벤트 처리를 한 프레임 기다린다.
        yield return null;
        float deadline = Time.realtimeSinceStartup + 10f;
        var wait = new WaitForSecondsRealtime(0.1f);
        while (PhotonNetwork.InRoom && !hasSpawned)
        {
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            if (manager.HasActor(actorNumber))
            {
                hasSpawned = true;
                break;
            }
            bool earlierReady = true;
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber < actorNumber && !manager.HasActor(player.ActorNumber))
                {
                    earlierReady = false;
                    break;
                }
            }
            if (earlierReady)
            {
                int slot = manager.FindFreeSlot();
                if (slot < 0)
                {
#if UNITY_EDITOR
                    Debug.LogError("[PhotonPlayerSpawnerForTest] 빈 시작 슬롯이 없어 생성하지 않습니다.", this);
#endif
                    break;
                }
                Vector3 offset = new Vector3((slot % 2 == 0 ? -1f : 1f) * slotSpacing, 0f, (slot < 2 ? -1f : 1f) * slotSpacing);
                Vector3 position = spawnOrigin.position + originOffset + offset;
                localPlayer = PhotonNetwork.Instantiate(c_prefabPath, position, spawnOrigin.rotation, 0, new object[] { slot });
                hasSpawned = localPlayer != null;
                break;
            }
            if (Time.realtimeSinceStartup >= deadline)
            {
#if UNITY_EDITOR
                Debug.LogError("[PhotonPlayerSpawnerForTest] 앞선 플레이어의 생성 등록을 10초 안에 확인하지 못했습니다.", this);
#endif
                break;
            }
            yield return wait;
        }
        spawnRoutine = null;
    }

    /// <summary>다음 명시적 방 입장을 위한 로컬 생성 상태를 비웁니다.</summary>
    private void ClearSpawn()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
        if (localPlayer != null)
        {
            PlayerNetworkSync sync = localPlayer.GetComponent<PlayerNetworkSync>();
            if (sync != null)
            {
                sync.SuspendLocalControl();
            }
        }
        localPlayer = null;
        hasSpawned = false;
    }
}
