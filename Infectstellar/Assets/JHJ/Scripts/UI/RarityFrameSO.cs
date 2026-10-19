using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "UI/Rarity Frame")]
public class RarityFrameSO : ScriptableObject
{
    [SerializeField] private List<RarityFrameEntry> entries = new List<RarityFrameEntry>();


    public bool TryGetFrame(ItemRarity rarity, out Sprite frame)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Rarity == rarity && entries[i].Frame != null)
            {
                frame = entries[i].Frame;
                return true;
            }
        }

        frame = null;
        return false;
    }

    private void OnValidate()
    {
        HashSet<ItemRarity> seen = new HashSet<ItemRarity>();

        for (int i = 0; i < entries.Count; i++)
        {
            RarityFrameEntry entry = entries[i];

            if (entry.Frame == null)
                Debug.LogWarning($"[RarityFrameSO] {i}번 줄: 테두리 스프라이트 없음 ({entry.Rarity})", this);

            if (!seen.Add(entry.Rarity))
                Debug.LogWarning($"[RarityFrameSO] {i}번 줄: 등급 중복 ({entry.Rarity})", this);
        }

        foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
        {
            if (!seen.Contains(rarity))
                Debug.LogWarning($"[RarityFrameSO] 등급 누락: {rarity}", this);
        }
    }
}
