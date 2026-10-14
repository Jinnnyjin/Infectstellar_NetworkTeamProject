using UnityEngine;

public class DamageSource : MonoBehaviour
{
    public float Damage { get; private set; }

    public void Init(float damage)
    {
        Damage = damage;
    }

    public void DealDamage(IDamageable damageable)
    {
        damageable.TakeDamage(new DamageInfo(Damage));
    }
}
