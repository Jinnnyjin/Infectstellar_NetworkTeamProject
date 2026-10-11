using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 외부 초기화로 체력을 관리하고 변경·피격·회복·사망을 알리는 독립 컴포넌트입니다.
/// 개체의 부활 준비, 피해 계산, 사망 반응은 외부에서 담당합니다.
/// </summary>
public sealed class _Health : MonoBehaviour
{
    private float currentHealth;
    private float maxHealth;
    private bool isInitialized;
    private bool isDead;
    private readonly Queue<Action> pendingChanges = new Queue<Action>();
    private bool isNotifying;
    private bool isDrainingChanges;

    /// <summary>현재 체력입니다. 초기화 전에는 0입니다.</summary>
    public float CurrentHealth => currentHealth;

    /// <summary>최대 체력입니다. 초기화 전에는 0입니다.</summary>
    public float MaxHealth => maxHealth;

    /// <summary>유효한 값으로 한 번 이상 초기화되었는지 나타냅니다.</summary>
    public bool IsInitialized => isInitialized;

    /// <summary>초기화 후 체력이 0이 되었는지 나타냅니다. 초기화 전에는 false입니다.</summary>
    public bool IsDead => isDead;

    /// <summary>생존 중 피격을 알립니다. 인수는 요청 피해 정보와 실제 체력 감소량입니다.</summary>
    public event Action<DamageInfo, float> D_Damaged;

    /// <summary>체력 상태 변경을 알립니다. 인수 순서는 이전 체력, 현재 체력, 최대 체력입니다.</summary>
    public event Action<float, float, float> D_HealthChanged;

    /// <summary>체력이 실제 증가했을 때 회복량을 알립니다.</summary>
    public event Action<float> D_Healed;

    /// <summary>한 생명당 한 번, 사망을 유발한 피해 정보를 알립니다.</summary>
    public event Action<DamageInfo> D_Died;

    /// <summary>
    /// 양수인 유한한 값으로 최대·현재 체력을 설정하고 사망 상태를 해제합니다.
    /// 최초 초기화 또는 체력 상태가 변경되었을 때 D_HealthChanged를 발생시킵니다.
    /// 이벤트 구독 중 호출하면 현재 알림 묶음이 끝난 뒤 호출 순서대로 처리합니다.
    /// </summary>
    public void Initialize(float health)
    {
        if (isNotifying)
        {
            pendingChanges.Enqueue(() => Initialize(health));
            return;
        }

        if (!IsFiniteNonNegative(health) || health == 0f)
        {
#if UNITY_EDITOR
            Debug.LogError($"[Health] 초기 체력은 양수인 유한한 값이어야 합니다. 입력값: {health}", this);
#endif
            return;
        }

        float previousHealth = currentHealth;
        bool hasChanged = !isInitialized || currentHealth != health || maxHealth != health;
        maxHealth = health;
        currentHealth = health;
        isInitialized = true;
        isDead = false;

        if (hasChanged)
        {
            NotifyChange(() => D_HealthChanged?.Invoke(previousHealth, health, health));
        }
    }

    /// <summary>공격자와 피격 위치 없이 최종 피해량을 적용합니다.</summary>
    public void TakeDamage(float amount)
    {
        TakeDamage(new DamageInfo(amount));
    }

    /// <summary>
    /// 생존 중 최종 피해를 적용하고 D_Damaged → D_HealthChanged → D_Died 순서로 알립니다.
    /// 0 피해는 D_Damaged만 발생시키며 사망 후 호출은 무시합니다.
    /// 이벤트 구독 중 호출하면 현재 알림 묶음이 끝난 뒤 호출 순서대로 처리합니다.
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (isNotifying)
        {
            pendingChanges.Enqueue(() => TakeDamage(damageInfo));
            return;
        }

        if (!CanChangeHealth(damageInfo.Amount))
        {
            return;
        }

        float previousHealth = currentHealth;
        float nextHealth = Mathf.Max(0f, previousHealth - damageInfo.Amount);
        float actualDamage = previousHealth - nextHealth;
        float damageMaxHealth = maxHealth;
        bool died = nextHealth == 0f;
        currentHealth = nextHealth;
        isDead = died;

        NotifyChange(() =>
        {
            D_Damaged?.Invoke(damageInfo, actualDamage);

            if (actualDamage > 0f)
            {
                D_HealthChanged?.Invoke(previousHealth, nextHealth, damageMaxHealth);
            }

            if (died)
            {
                D_Died?.Invoke(damageInfo);
            }
        });
    }

    /// <summary>
    /// 생존 중 최대 체력까지 회복하고 D_Healed → D_HealthChanged 순서로 알립니다.
    /// 이벤트 구독 중 호출하면 현재 알림 묶음이 끝난 뒤 호출 순서대로 처리합니다.
    /// </summary>
    public void Heal(float amount)
    {
        if (isNotifying)
        {
            pendingChanges.Enqueue(() => Heal(amount));
            return;
        }

        if (!CanChangeHealth(amount))
        {
            return;
        }

        float previousHealth = currentHealth;
        float nextHealth = Mathf.Min(maxHealth, previousHealth + amount);
        float actualHealing = nextHealth - previousHealth;
        if (actualHealing <= 0f)
        {
            return;
        }

        float healingMaxHealth = maxHealth;
        currentHealth = nextHealth;
        NotifyChange(() =>
        {
            D_Healed?.Invoke(actualHealing);
            D_HealthChanged?.Invoke(previousHealth, nextHealth, healingMaxHealth);
        });
    }

    private void NotifyChange(Action notification)
    {
        isNotifying = true;
        try
        {
            notification();
        }
        catch
        {
            // 구독자 예외는 전파하고, 완료되지 않은 알림의 후속 요청은 남기지 않습니다.
            pendingChanges.Clear();
            throw;
        }
        finally
        {
            isNotifying = false;
        }

        if (isDrainingChanges)
        {
            return;
        }

        isDrainingChanges = true;
        try
        {
            while (pendingChanges.Count > 0)
            {
                pendingChanges.Dequeue().Invoke();
            }
        }
        finally
        {
            isDrainingChanges = false;
        }
    }

    private bool CanChangeHealth(float amount)
    {
        if (!isInitialized)
        {
#if UNITY_EDITOR
            Debug.LogError("[Health] 피해 또는 회복 전에 Initialize를 호출해야 합니다.", this);
#endif
            return false;
        }

        if (!IsFiniteNonNegative(amount))
        {
#if UNITY_EDITOR
            Debug.LogError($"[Health] 피해·회복량은 0 이상인 유한한 값이어야 합니다. 입력값: {amount}", this);
#endif
            return false;
        }

        return !isDead;
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
