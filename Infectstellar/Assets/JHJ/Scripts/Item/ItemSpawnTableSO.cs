using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Item/SpawnTable", fileName = "SpawnTable")]
public class ItemSpawnTableSO : ScriptableObject
{
    [SerializeField] private RarityWeight[] rarityWeights;
    [SerializeField] private Item[] itemPrefabs;

    public IReadOnlyList<RarityWeight> RarityWeights => rarityWeights;
    public IReadOnlyList<Item> ItemPrefabs => itemPrefabs;
}
