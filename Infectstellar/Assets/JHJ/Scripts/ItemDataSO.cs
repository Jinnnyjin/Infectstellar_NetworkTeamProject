using UnityEngine;

[CreateAssetMenu(menuName = "Item/Data", fileName ="Item_")]
public class ItemDataSO : ScriptableObject
{
    public const float MAX_WEIGHT = 20.0f; // [미정] 테스트하며 조정 예정
    public const float MIN_WEIGHT = 1f; // [미정] 테스트하며 조정 예정

    [Tooltip("1부터 시작, 0은 미지정")] [Min(1)]
    [SerializeField] private int id;
    [SerializeField] private string itemName;
    [SerializeField] private int price;
    [SerializeField] private float attack;
    [Tooltip("단위 kg")] [Range(MIN_WEIGHT, MAX_WEIGHT)]
    [SerializeField] private float weight;

    public int Id => id;
    public string ItemName => itemName;
    public int Price => price;
    public float Attack => attack;
    public float Weight => weight;
}
