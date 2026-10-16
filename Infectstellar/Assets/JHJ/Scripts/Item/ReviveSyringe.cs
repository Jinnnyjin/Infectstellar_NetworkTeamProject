using UnityEngine;

public class ReviveSyringe : MonoBehaviour, IUsable
{
    public bool TryUse(HitContext hitContext)
    {
        if (!hitContext.Target.TryGetComponent<PlayerStateMachine>(out PlayerStateMachine stateMachine))
            return false;

        if (stateMachine.IsAlive)
            return false;

        Revive(hitContext.Target);

        Debug.Log($"[ReviveSyringe] {hitContext.Target.name} 부활");
        return true;
    }

    private void Revive(GameObject target)
    {
        // [임시] Health.OnRevived 구현 전까지 상태만 전환.
        // → OnRevived로 교체 필요
        if (target.TryGetComponent<PlayerStateMachine>(out PlayerStateMachine stateMachine))
        {
            stateMachine.Revive();
        }

        // 나중: target.GetComponent<Health>().OnRevived();
    }
}
