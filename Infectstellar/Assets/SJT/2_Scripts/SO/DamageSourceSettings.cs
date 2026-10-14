using UnityEngine;

[CreateAssetMenu(fileName = "DamageSourceSettings", menuName = "Scriptable Objects/DamageSourceData")]
public class DamageSourceSettings : ScriptableObject
{
    [SerializeField, Min(0f), Tooltip("Entity에게 주는 데미지의 양")] private float damageAmount = 10f;
    public float DamageAmount => damageAmount;
}
