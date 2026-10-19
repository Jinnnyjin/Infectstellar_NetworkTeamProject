using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPointRoot;
    [SerializeField] private ItemSpawnTableSO spawnTable;
    [Min(0)]
    [SerializeField] private int minSpawnCount;
    [Min(0)]
    [SerializeField] private int maxSpawnCount;


    [ContextMenu("SpawnItem")]
    public void Spawn()
    {
        if (!ValidateNetwork())
            return;

        // 스폰포인트 부모, 테이블 미연결 확인 / 포인트 개수 최소값, 최대값 확인
        if (!ValidateSettings())
            return;

        ItemSpawnPoint[] spawnPoints = spawnPointRoot.GetComponentsInChildren<ItemSpawnPoint>();

        if (spawnPoints.Length == 0)
        {
            Debug.LogWarning($"[ItemSpawner] Spawn 불가 : spawnPoints가 없음.", this);
            return;
        }

        int spawnCount = Random.Range(minSpawnCount, maxSpawnCount + 1);

        if (spawnCount > spawnPoints.Length)
        {
            Debug.LogWarning($"[ItemSpawner] 뽑힌 개수 {spawnCount}가 스폰 포인트 {spawnPoints.Length}개보다 많아 포인트 수로 제한", this);

            spawnCount = spawnPoints.Length;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            int j = Random.Range(i, spawnPoints.Length);

            ItemSpawnPoint temp = spawnPoints[i];

            spawnPoints[i] = spawnPoints[j];
            spawnPoints[j] = temp;
        }


        // 등급별 아이템 목록 생성
        Dictionary<ItemRarity, List<Item>> itemsByRarity = new Dictionary<ItemRarity, List<Item>>();

        foreach (Item prefab in spawnTable.ItemPrefabs)
        {

            if (prefab == null)
            {
                Debug.LogWarning("[ItemSpawner] 스폰 테이블에 빈 칸이 있음", this);
                continue;
            }


            ItemRarity itemRarity = prefab.ItemData.Rarity;

            if (!itemsByRarity.ContainsKey(itemRarity))
                itemsByRarity[itemRarity] = new List<Item>();

            itemsByRarity[itemRarity].Add(prefab);
        }

        // 선택된 스폰 포인트마다 등급에 따라서 아이템 생성
        for (int i = 0; i < spawnCount; i++)
        {
            if (!TryPickRarity(out ItemRarity rarity))
                return;

            if (!itemsByRarity.TryGetValue(rarity, out List<Item> items))
            {
                Debug.LogWarning($"[ItemSpawner] {rarity} 등급 아이템이 테이블에 없음, {spawnPoints[i].name} 건너뜀", this);
                continue;
            }

            Item prefab = items[Random.Range(0, items.Count)];
            Quaternion rot = Quaternion.identity;

            if(!RoomItemSpawnUtil.TryGetRootPositionOnFloor(prefab, spawnPoints[i].transform.position, rot, out Vector3 pos))
            {
                Debug.LogWarning($"[ItemSpawner] 바닥 중심 계산 실패 / 프리팹 이름: {prefab.name}, 스폰 위치: {spawnPoints[i].name}", this);
                continue;
            }

            RoomItemSpawnUtil.InstantiateRoomItem(prefab, pos, rot);
            Debug.Log($"[ItemSpawner] 스폰: {spawnPoints[i].name}에 {prefab.ItemData.ItemName} ({rarity})", this);
        }

    }

    /// <summary>
    /// 방 안의 방장인지 확인((스폰·라운드 정리 등 방장 전용 작업 공용)
    /// </summary>
    private bool ValidateNetwork()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[ItemSpawner] 방장 전용 작업 불가: 방에 들어와 있지 않음", this);
            return false;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[ItemSpawner] 방장 전용 작업 불가: 방장이 아님", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 인스펙터 설정이 스폰 가능한 상태인지
    /// </summary>
    private bool ValidateSettings()
    {
        if (spawnPointRoot == null || spawnTable == null)
        {
            Debug.LogError($"[ItemSpawner] Spawn 불가 : spawnPointRoot 또는 spawnTable 확인 불가", this);
            return false;
        }

        if (minSpawnCount > maxSpawnCount)
        {
            Debug.LogWarning($"[ItemSpawner] Spawn 불가 : minSpawnCount가 maxSpawnCount보다 많음. min = {minSpawnCount}, max = {maxSpawnCount}", this);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 스폰테이블의 가중치에 따라 등급 뽑기
    /// </summary>
    private bool TryPickRarity(out ItemRarity rarity)
    {
        rarity = default;

        int total = 0;

        foreach (RarityWeight w in spawnTable.RarityWeights)
        {
            total += w.Weight;
        }

        if (total <= 0)
        {
            Debug.LogWarning("[ItemSpawner] 등급 뽑기 불가: 가중치 합이 0", this);
            return false;
        }


        int roll = Random.Range(0, total);
        int cumulative = 0;

        foreach (RarityWeight w in spawnTable.RarityWeights)
        {
            cumulative += w.Weight;

            if (roll < cumulative)
            {
                rarity = w.Rarity;

                return true;
            }
        }

        Debug.LogError("[ItemSpawner] 등급 뽑기 실패: 구간을 찾지 못함", this);
        return false;

    }
}
