public readonly struct InteractableInfo
{
    public string ItemName { get; }
    public int Price { get; }
    
    public InteractableInfo(string itemName, int price)
    {
        ItemName = itemName;
        Price = price;
    }
}