
using System;
using UnityEngine;

[Serializable]
public struct RarityWeight 
{
    [SerializeField] private ItemRarity rarity;
    
    [Min(0)]
    [SerializeField] private int weight;


    public ItemRarity Rarity => rarity;
    public int Weight => weight;

}
