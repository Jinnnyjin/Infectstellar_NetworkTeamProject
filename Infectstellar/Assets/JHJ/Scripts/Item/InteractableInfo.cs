public readonly struct InteractableInfo
{
    public string ItemName { get; }
    public int Price { get; }
    public ItemRarity Rarity { get; }
    
    public InteractableInfo(string itemName, int price, ItemRarity rarity)
    {
        ItemName = itemName;
        Price = price;
        Rarity = rarity;
    }
}