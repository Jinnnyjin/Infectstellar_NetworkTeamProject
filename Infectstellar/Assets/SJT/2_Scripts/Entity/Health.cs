using System;
using UnityEngine;
using UnityEngine.Events;

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
    /// <summary>직접 실행한 부활이 적용된 뒤 RPC 송신용으로 알립니다.</summary>
    public event Action D_ReviveApplied;
    public event Action D_Revived;

    public UnityEvent KHJ_OnDie;
    [SerializeField]
    private PlayerDataObserver KHJ_PlayerDataObserver;

    /// <summary>최대 체력과 현재 체력을 초기화합니다.</summary>
    private void Start()
    {
        if (maxHP <= 0f)
        {
            Initialize(100f);
        }
    }

    /// <summary>생성 시 사용할 최대 체력과 현재 체력을 함께 설정합니다.</summary>
    public void Initialize(float maxHealth)
    {
        if (!IsValidDamage(maxHealth))
        {
            throw new ArgumentOutOfRangeException(nameof(maxHealth));
        }

        maxHP = maxHealth;
        hp = maxHealth;
    }

#if UNITY_EDITOR
    [ContextMenu("TakeDamage 100")]
    private void KillPlayer()
    {
        TakeDamage(new DamageInfo(200f));
    }
#endif

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

        // 로컬에서만 실행되는 현준님 이벤트
        KHJ_OnDie?.Invoke();

        if (KHJ_PlayerDataObserver == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[Health] : PlayerDataObserver 가 존재하지 않습니다. 현재 체력 전달에 실패했습니다.");
#endif
            return false;
        }
        else
        {
            KHJ_PlayerDataObserver.SetPlayerCurrentHp(HP);
        }

        return true;
    }

    /// <summary>부활을 적용하고 RPC 송신용 알림을 발생시킵니다.</summary>
    public bool Revive()
    {
        if (!ApplyRevive())
        {
            return false;
        }

        D_ReviveApplied?.Invoke();
        return true;
    }

    /// <summary>RPC 송신용 알림 없이 체력을 복구하고 부활을 알립니다.</summary>
    internal bool ApplyRevive()
    {
        if (!isActiveAndEnabled || hp > 0f)
        {
            return false;
        }

        hp = maxHP;
        KHJ_PlayerDataObserver?.SetPlayerCurrentHp(HP);
        D_Revived?.Invoke();
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
