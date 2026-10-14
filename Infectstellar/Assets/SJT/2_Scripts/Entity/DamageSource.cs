using UnityEngine;

public class DamageSource : MonoBehaviour
{
    [SerializeField] DamageSourceSettings damageSourceSettings;
    private float damage;

    void Awake()
    {
        damage = damageSourceSettings.DamageAmount;
    }

    public void DealDamage(IDamageable damageable)
    {
        damageable.TakeDamage(new DamageInfo(damage));
    }
}
