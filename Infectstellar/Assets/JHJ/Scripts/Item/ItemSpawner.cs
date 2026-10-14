using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField] private Transform itemSpawnPoint;
    [SerializeField] private ItemSpawnTableSO spawnTable;
    [SerializeField] private int minSpawnCount;
    [SerializeField] private int maxSpawnCount;


    public void Spawn()
    {
        Transform[] itemSpawnPoints = itemSpawnPoint.GetComponentsInChildren<Transform>();

        // min, max 사이에서 값 랜덤
        int spawnCount = Random.Range(minSpawnCount, maxSpawnCount+1);

        // 포인트 중 해당 값 사이에 골라서

        // 생성시키기
    }
}
