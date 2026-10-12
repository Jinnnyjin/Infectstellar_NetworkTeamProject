using UnityEngine;
/// <summary>
/// 아이템 적중 판정 : 히트박스 활성, isHit(1번), TakeDamage호출
/// </summary>
public class ItemHit : MonoBehaviour
{
    private bool isActive;
    private bool isHit;
    private float damage;
    private int attackerActorNumber;

    public void Activate(float damage, int attackerActorNumber)
    {
        this.damage = damage;
        this.attackerActorNumber = attackerActorNumber;

        isHit = false;
        isActive = true;
    }

    public void Deactivate()
    {
        isActive = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isActive || isHit)
            return;

        // [추가 필요] 던진 본인이면 return

        isHit = true;

        ITempDamageable damageable = collision.collider.GetComponentInParent<ITempDamageable>();
        if (damageable == null)
        {
            Debug.Log($"[ItemHit] 첫Hit {collision.collider.name}, 데미지 대상 아님 → 이번 Shoot 종료", this);
            return;
        }

        damageable.TakeDamage(new TempDamageInfo(damage, attackerActorNumber));

        Debug.Log($"[ItemHit] 적중: {collision.collider.name}, 데미지 {damage}, 공격자 {attackerActorNumber}", this);
    }
}
