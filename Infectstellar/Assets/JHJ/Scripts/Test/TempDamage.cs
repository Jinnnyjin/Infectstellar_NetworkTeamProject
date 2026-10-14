

// [임시] 플레이어 쪽 DamageInfo 합치기 전까지 사용
// 통합 시 삭제 예정

public readonly struct TempDamageInfo
{
    public float Damage { get; }
    public int AttackerActorNumber { get; }

    public TempDamageInfo(float damage, int attackerActorNumber)
    {
        Damage = damage;
        AttackerActorNumber = attackerActorNumber;
    }
}
public interface ITempDamageable
{
    public void TakeDamage(TempDamageInfo info);
}
