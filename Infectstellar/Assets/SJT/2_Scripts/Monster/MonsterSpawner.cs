using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>마스터의 명시적 호출로 하위 몬스터 지점을 룸마다 한 번 생성합니다.</summary>
[DisallowMultipleComponent]
public sealed class MonsterSpawner : MonoBehaviourPunCallbacks
{
    private const string c_spawnStartedKey = "SJT.MonsterSpawnStarted";

    private Room pendingRoom;
    private SpawnEntry[] pendingEntries;
    private int pendingActor;

    /// <summary>룸의 생성 시작 기록을 확인받은 뒤 모든 활성 지점에 한 마리씩 생성합니다.</summary>
    public void SpawnAll()
    {
        Room room = PhotonNetwork.CurrentRoom;
        if (!isActiveAndEnabled || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || pendingRoom != null ||
            room.CustomProperties.ContainsKey(c_spawnStartedKey))
        {
            return;
        }
        if (!PhotonNetwork.OfflineMode && !room.BroadcastPropertiesChangeToAll)
        {
#if UNITY_EDITOR
            Debug.LogError("[MonsterSpawner] 서버의 생성 시작 확인을 받으려면 룸의 BroadcastPropsChangeToAll이 켜져 있어야 합니다.", this);
#endif
            return;
        }
        if (!TryPrepareSpawns(out SpawnEntry[] entries))
        {
            return;
        }

        pendingRoom = room;
        pendingEntries = entries;
        pendingActor = PhotonNetwork.LocalPlayer.ActorNumber;
        // 전송 성공은 서버 반영 완료가 아니므로 OnRoomPropertiesUpdate에서 생성합니다.
        if (!room.SetCustomProperties(new Hashtable { [c_spawnStartedKey] = pendingActor }))
        {
            ClearRequest();
#if UNITY_EDITOR
            Debug.LogError("[MonsterSpawner] 룸의 생성 시작 기록을 전송하지 못했습니다.", this);
#endif
        }
    }

    /// <summary>하위의 활성 지점을 모두 검사하고 요청 시점의 위치·Y 회전을 보관합니다.</summary>
    private bool TryPrepareSpawns(out SpawnEntry[] entries)
    {
        entries = null;
        MonsterSpawnPoint[] points = GetComponentsInChildren<MonsterSpawnPoint>();
        List<SpawnEntry> prepared = new List<SpawnEntry>(points.Length);
        foreach (MonsterSpawnPoint point in points)
        {
            if (!point.isActiveAndEnabled)
            {
                continue;
            }
            if (!point.TryGetPrefabPath(out string prefabPath))
            {
                return false;
            }
            Transform pointTransform = point.transform;
            prepared.Add(new SpawnEntry(prefabPath, pointTransform.position, Quaternion.Euler(0f, pointTransform.eulerAngles.y, 0f)));
        }
        if (prepared.Count == 0)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[MonsterSpawner] 하위에 활성 몬스터 스폰 지점이 없습니다.", this);
#endif
            return false;
        }
        entries = prepared.ToArray();
        return true;
    }

    /// <summary>현재 요청자의 룸 기록이 확인된 경우에만 대기 없이 순회 생성합니다.</summary>
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (pendingRoom == null || !propertiesThatChanged.ContainsKey(c_spawnStartedKey))
        {
            return;
        }
        bool canSpawn = isActiveAndEnabled && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient &&
            ReferenceEquals(pendingRoom, PhotonNetwork.CurrentRoom) &&
            propertiesThatChanged[c_spawnStartedKey] is int actor && actor == pendingActor;
        SpawnEntry[] entries = pendingEntries;
        Room room = pendingRoom;
        ClearRequest();
        if (!canSpawn)
        {
            return;
        }
        foreach (SpawnEntry entry in entries)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || !ReferenceEquals(room, PhotonNetwork.CurrentRoom))
            {
                return;
            }
            if (PhotonNetwork.InstantiateRoomObject(entry.prefabPath, entry.position, entry.rotation) == null)
            {
#if UNITY_EDITOR
                Debug.LogError("[MonsterSpawner] 몬스터 생성에 실패해 남은 생성을 중단합니다. 룸의 생성 시작 기록은 유지합니다.", this);
#endif
                return;
            }
        }
    }

    /// <summary>마스터가 바뀌면 이전 요청을 취소하며 자동 재생성하지 않습니다.</summary>
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        ClearRequest();
    }

    /// <summary>룸 이탈 시 이전 룸의 대기 요청을 폐기합니다.</summary>
    public override void OnLeftRoom()
    {
        ClearRequest();
    }

    /// <summary>연결 종료 시 대기 요청을 폐기합니다.</summary>
    public override void OnDisconnected(DisconnectCause cause)
    {
        ClearRequest();
    }

    /// <summary>비활성화 시 대기 요청과 PUN 콜백 구독을 정리합니다.</summary>
    public override void OnDisable()
    {
        ClearRequest();
        base.OnDisable();
    }

    /// <summary>룸의 생성 시작 기록은 유지하고 로컬 요청만 비웁니다.</summary>
    private void ClearRequest()
    {
        pendingRoom = null;
        pendingEntries = null;
        pendingActor = 0;
    }

    /// <summary>서버 확인을 기다리는 동안 유지할 한 지점의 생성 정보입니다.</summary>
    private readonly struct SpawnEntry
    {
        public readonly string prefabPath;
        public readonly Vector3 position;
        public readonly Quaternion rotation;

        /// <summary>검사한 프리팹 경로와 출현 자세를 저장합니다.</summary>
        public SpawnEntry(string prefabPath, Vector3 position, Quaternion rotation)
        {
            this.prefabPath = prefabPath;
            this.position = position;
            this.rotation = rotation;
        }
    }
}
