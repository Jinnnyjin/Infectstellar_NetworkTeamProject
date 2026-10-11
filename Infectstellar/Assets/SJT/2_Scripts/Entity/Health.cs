using System;
using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    #region 멤버 변수, 프로퍼티
    private float currentHealth;
    private float maxHealth;
    private bool isInitialized;
    private bool isDead;
    private readonly Queue<Action> pendingChanges = new();
    private bool isNotifying;
    private bool isDrainingChanges;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    #endregion

    #region Action 이벤트
    /// <summary>생존 중 피격 알림</summary>
    /// <param_1>요청 피해 정보</param>
    /// <param_2>실제 체력 감소량</param>
    public event Action<DamageInfo, float> D_Damaged;
    /// <summary>체력 상태 변경 알림</summary>
    /// <param_1>이전 체력</param>
    /// <param_2>현재 체력</param>
    /// <param_3>최대 체력</param>
    public event Action<float, float, float> D_HealthChanged;
    /// <summary>체력이 실제 증가했을 때 알림</summary>
    /// <param_1>회복량</param>
    public event Action<float> D_Health;
    /// <summary>사망 알림</summary>
    /// <param_1>사망을 유발한 피해 정보</param>
    public event Action<DamageInfo> D_Died;
    #endregion

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

    /// <summary>
    /// 공격자와 피격 위치 없이 최종 피해량을 적용합니다.
    /// </summary>
    /// <param name="amount">전달받은 피해량</param>
    public void TakeDamage(float amount)
    {
        TakeDamage(new DamageInfo(amount));
    }

    /// <summary>
    /// 생존 중 최종 피해를 적용하고 D_Damaged → D_HealthChanged → D_Died 순서로 알림
    /// </summary>
    /// <param name="damageInfo">데미지 정보</param>
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
    }
}
