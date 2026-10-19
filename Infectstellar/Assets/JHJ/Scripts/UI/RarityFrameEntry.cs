using System;
using UnityEngine;

[Serializable]
public struct RarityFrameEntry
{
    [SerializeField] private ItemRarity rarity;
    [SerializeField] private Sprite frame;

    public ItemRarity Rarity => rarity;
    public Sprite Frame => frame;
}
