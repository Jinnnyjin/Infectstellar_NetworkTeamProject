using System;
using UnityEngine;

[Serializable]
public struct ShopEntry
{
    [SerializeField] private Item item;
    [SerializeField, Min(1)] private int buyPrice;

    public Item Item => item;
    public int BuyPrice => buyPrice;
}
