using UnityEngine;

public readonly struct HitContext
{
    public readonly GameObject Target;
    public readonly int ShooterActorNumber;

    public HitContext(GameObject target, int shooterActorNumber)
    {
        Target = target;
        ShooterActorNumber = shooterActorNumber;
    }
}
