using System;
using UnityEngine;
/// <summary>
/// 아이템 적중 판정 : 히트박스 활성, isHit(1번), TakeDamage호출
/// </summary>
[RequireComponent(typeof(DamageSource))]
public class ItemHit : MonoBehaviour
{
    private bool isActive;
    private bool isHit;
    private int attackerActorNumber;
    private bool isLocalThrower;
    private IUsable usable;

    private Func<Collider, int> getOwnerActorNumber;
    private Action onUsed;

    #region 심지태
    private DamageSource damageSource;
    #endregion


    public void Init(Func<Collider, int> GetOwnerActorNumber, ItemDataSO itemDataSO, Action onUsed)
    {
        this.getOwnerActorNumber = GetOwnerActorNumber;
        this.onUsed = onUsed;

        IUsable[] usables = GetComponents<IUsable>();

        if (usables.Length > 1)
        {
            Debug.LogWarning($"[ItemHit] {name}에 IUsable이 {usables.Length}개 있음. 첫 번째만 사용", this);
        }

        usable = usables.Length > 0 ? usables[0] : null;

        if (!gameObject.TryGetComponent<DamageSource>(out damageSource))
        {
#if UNITY_EDITOR
            Debug.LogError($"ItemHit : 오브젝트에 DamageSource가 없습니다.");
#endif
            return;
        }

        damageSource.Init(itemDataSO.Attack);
    }

    public void Activate(int attackerActorNumber, bool isLocalThrower)
    {
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
        Debug.Log($"collision : {collision.gameObject.name}"); // [임시]

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

        // 사용가능한 아이템(부활 주사기 등) 먼저 판정
        if(usable != null && usable.TryUse(new HitContext(collision.gameObject, attackerActorNumber)))
        {
            Debug.Log($"[ItemHit] Usable TryUse 사용 : {collision.gameObject.name}, 사용자 {attackerActorNumber}", this);
            onUsed?.Invoke();
            return;
        }

        // 사용가능한 아이템이 아니면 공격 데미지
        Debug.Log($"[ItemHit] 적중: {collision.collider.name}, 데미지 {damageSource.Damage}, 공격자 {attackerActorNumber}", this);

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
