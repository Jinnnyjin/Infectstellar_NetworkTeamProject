using System;
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
    private bool isLocalThrower;
    private Func<Collider, int> getOwnerActorNumber;

    #region 심지태
    private DamageSource damageSource;

    private void Awake()
    {
        if (!gameObject.TryGetComponent<DamageSource>(out DamageSource _damageSource))
        {
#if UNITY_EDITOR
            Debug.LogError($"ItemHit : 오브젝트에 DamageSource가 없습니다.");
#endif
            return;
        }
        else
        {
            damageSource = _damageSource;
        }
    }
    #endregion


    public void Init(Func<Collider, int> GetOwnerActorNumber)
    {
        this.getOwnerActorNumber = GetOwnerActorNumber;
    }

    public void Activate(float damage, int attackerActorNumber, bool isLocalThrower)
    {
        this.damage = damage;
        this.attackerActorNumber = attackerActorNumber;
        this.isLocalThrower = isLocalThrower;

        isHit = false;
        isActive = true;
    }

    public void Deactivate()
    {
        isActive = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"collision : {collision.gameObject.name}");

        // 활성화 상태 아니거나 이미 맞은 경우 무시
        if (!isActive || isHit)
            return;

        // 던진사람 컴퓨터에서만 판정, 나머지 컴퓨터는 물리 반응만
        if (!isLocalThrower)
            return;

        // 본인이 맞은 경우 무시
        if (getOwnerActorNumber(collision.collider) == attackerActorNumber)
            return;

        isHit = true;
        Debug.Log($"[ItemHit] 적중: {collision.collider.name}, 데미지 {damage}, 공격자 {attackerActorNumber}", this);

        #region 심지태
        if (damageSource == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"ItemHit : 오브젝트에 DamageSource가 없습니다.");
#endif
            return;
        }

        if (collision.gameObject.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            damageSource.DealDamage(damageable);
#if UNITY_EDITOR
            Debug.Log($"때린거 : {damageSource.gameObject.name}");
#endif
        }
        #endregion
    }
}
