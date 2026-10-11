using UnityEngine;

/// <summary>
/// 외부에서 계산한 최종 피해량과 선택적인 공격자·월드 피격 위치입니다.
/// </summary>
public readonly struct DamageInfo
{
    private readonly float amount;
    private readonly GameObject attacker;
    private readonly Vector3? hitPoint;

    /// <summary>방어·저항 계산이 끝난 요청 피해량입니다.</summary>
    public float Amount => amount;

    /// <summary>피해의 공격자입니다. 출처가 없으면 null입니다.</summary>
    public GameObject Attacker => attacker;

    /// <summary>월드 좌표의 피격 위치입니다. 특정 지점이 없으면 null입니다.</summary>
    public Vector3? HitPoint => hitPoint;

    public DamageInfo(float amount, GameObject attacker = null, Vector3? hitPoint = null)
    {
        this.amount = amount;
        this.attacker = attacker;
        this.hitPoint = hitPoint;
    }
}
