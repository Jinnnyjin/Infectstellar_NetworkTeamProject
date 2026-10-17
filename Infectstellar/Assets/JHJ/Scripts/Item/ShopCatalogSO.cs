using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName ="Shop/Catalog", fileName ="Catalog_")]
public class ShopCatalogSO : ScriptableObject
{
    [SerializeField] private List<ShopEntry> entries = new List<ShopEntry>();

    public IReadOnlyList<ShopEntry> Entries => entries;

    public bool TryGetEntry(int index, out ShopEntry entry)
    {
        if (index < 0 || index >= entries.Count)
        {
            entry = default;
            return false;
        }

        entry = entries[index];
        return true;
    }

    private void OnValidate()
    {
        if (entries == null)
            return;

        HashSet<Item> seen = new HashSet<Item>();

        for (int i = 0; i < entries.Count; i++)
        {
            ShopEntry entry = entries[i];

            if (entry.Item == null)
            {
                Debug.LogWarning($"[ShopCatalogSO] {i}번줄: 아이템 비어있음", this);
                continue;
            }

            if (!seen.Add(entry.Item))
            {
                Debug.LogWarning($"[ShopCatalogSO] {i}번줄: 중복아이템", this);
            }

            if (entry.Item.ItemData == null)
            {
                Debug.LogWarning($"[ShopCatalogSO] {i}번줄: 아이템 데이터 비어있음", this);
                continue;
            }

            if(entry.BuyPrice <= entry.Item.ItemData.Price)
            {
                Debug.LogWarning($"[ShopCatalogSO] {i}번줄: 아이템 구매가({entry.BuyPrice})가 판매가({entry.Item.ItemData.Price}) 이하", this);
            }
        }


    }
}
