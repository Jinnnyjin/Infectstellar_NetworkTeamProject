using UnityEngine;

public class ReviveSyringe : MonoBehaviour, IUsable
{
    public bool TryUse(HitContext hitContext)
    {
        if (!hitContext.Target.TryGetComponent<PlayerController>(out PlayerController controller))
            return false;

        if (!Revive(controller))
            return false;

        Debug.Log($"[ReviveSyringe] {hitContext.Target.name} 부활");
        return true;
    }

    private bool Revive(PlayerController targetController)
    {

        return targetController.TryRevive();

    }
}
