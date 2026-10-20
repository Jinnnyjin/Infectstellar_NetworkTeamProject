using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName ="Shop/Product", fileName ="Product_")]
public class ShopProductSO : ScriptableObject
{
    [SerializeField] private Item item;
    [SerializeField, Min(1)] private int buyPrice;

    public Item Item => item;
    public int BuyPrice => buyPrice;



    private void OnValidate()
    {
        if (item == null)
        {
            Debug.LogWarning($"[ShopProductSO] 아이템 비어있음", this);
            return;
        }

        if (item.ItemData == null)
        {
            Debug.LogWarning($"[ShopProductSO] 아이템 데이터 비어있음", this);
            return;
        }

        if (buyPrice <= item.ItemData.Price)
            Debug.LogWarning($"[ShopProductSO] 구매가({buyPrice})가 판매가({item.ItemData.Price}) 이하", this);
    }


}
