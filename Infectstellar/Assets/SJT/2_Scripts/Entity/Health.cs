using System;
using UnityEngine;

/// <summary>체력을 관리하고 최초 피해 적용 결과를 알립니다.</summary>
public class Health : MonoBehaviour, IDamageable
{
    private float maxHP;
    private float hp = 100f;

    /// <summary>현재 체력입니다.</summary>
    public float HP => hp;

    /// <summary>TakeDamage로 피해가 실제 적용된 뒤 알립니다.</summary>
    public event Action<DamageInfo> D_DamageApplied;
    /// <summary>피해로 체력이 0이 되었을 때 한 번 알립니다.</summary>
    public event Action<DamageInfo> D_Died;

    /// <summary>최대 체력과 현재 체력을 초기화합니다.</summary>
    private void Awake()
    {
        hp = 100f;
        maxHP = hp;
    }

    /// <summary>피해를 적용하고 성공했을 때 RPC 송신용 알림을 발생시킵니다.</summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!ApplyDamage(damageInfo))
        {
            return;
        }

        D_DamageApplied?.Invoke(damageInfo);
    }

    /// <summary>송신용 알림 없이 피해를 적용하고, 체력이 0이 되면 사망을 알립니다.</summary>
    internal bool ApplyDamage(DamageInfo damageInfo)
    {
        float amount = damageInfo.Amount;

        if (!isActiveAndEnabled || hp <= 0f || !IsValidDamage(amount))
        {
            return false;
        }

        float nextHP = Mathf.Max(0f, hp - amount);
        if (nextHP == hp)
        {
            return false;
        }

        hp = nextHP;

#if UNITY_EDITOR
        Debug.Log($"[Health] {amount}만큼 공격 받음. 현재 체력 : {hp}/{maxHP}", this);
#endif

        if (hp <= 0f)
        {
            D_Died?.Invoke(damageInfo);
        }

        return true;
    }

    /// <summary>피해량이 유한한 양수인지 확인합니다.</summary>
    internal static bool IsValidDamage(float amount)
    {
        return !float.IsNaN(amount)
            && !float.IsInfinity(amount)
            && amount > 0f;
    }
}