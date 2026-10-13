using UnityEngine;

/// <summary>
/// Entity가 데미지를 받는 단위 데이터
/// </summary>
public readonly struct DamageInfo
{
    private readonly float amount;

    /// <summary>플레이어에게 들어온 데미지 양</summary>
    public float Amount => amount;

    public DamageInfo(float amount)
    {
        this.amount = amount;
    }
}
