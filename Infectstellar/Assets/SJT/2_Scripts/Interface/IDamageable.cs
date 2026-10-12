using UnityEngine;

public interface IDamageable
{
    public void TakeDamage(float amount);
    public void TakeDamage(DamageInfo damageInfo);
}
