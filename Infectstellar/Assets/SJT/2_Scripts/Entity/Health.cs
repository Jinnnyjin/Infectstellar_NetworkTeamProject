using System;
using Photon.Pun;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    private float maxHP;
    private float hp = 100;
    private _PlayerNetworkSync networkSync;
    public float HP => hp;
    public event Action<float> D_HealthChanged;

    private void Awake()
    {
        hp = 100f;
        maxHP = hp;
        networkSync = GetComponent<_PlayerNetworkSync>();
    }

    /// <summary>네트워크 플레이어는 소유자에게 피해를 요청하고, 일반 객체는 직접 적용합니다.</summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (networkSync != null && PhotonNetwork.InRoom)
        {
            networkSync.RequestDamage(damageInfo.Amount);
            return;
        }
        ApplyDamage(damageInfo);
    }

    /// <summary>소유자가 확정한 피해를 한 번 적용하고 HP 변경을 알립니다. RPC를 다시 요청하지 않습니다.</summary>
    internal void ApplyDamage(DamageInfo damageInfo)
    {
        float amount = damageInfo.Amount;
        if (!isActiveAndEnabled || hp <= 0f || !IsValidDamage(amount))
        {
            return;
        }
        hp = Mathf.Max(0f, hp - amount);
        D_HealthChanged?.Invoke(hp);
#if UNITY_EDITOR
        Debug.Log($"[Health] 플레이어 {(networkSync != null ? networkSync.ActorNumber : 0)}이 {amount}만큼 공격 받음. 현재 체력 : {hp}/{maxHP}", this);
#endif
    }

    /// <summary>원격에서는 피해를 재계산하지 않고 소유자가 보낸 최종 HP를 적용합니다.</summary>
    internal bool ApplyNetworkHP(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > maxHP)
        {
            return false;
        }
        if (hp == value)
        {
            return true;
        }
        hp = value;
        D_HealthChanged?.Invoke(hp);
        return true;
    }

    /// <summary>피해량은 유한한 양수만 허용합니다.</summary>
    internal static bool IsValidDamage(float amount)
    {
        return !float.IsNaN(amount) && !float.IsInfinity(amount) && amount > 0f;
    }
}
