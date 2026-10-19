using UnityEngine;

public class ReviveSyringe : MonoBehaviour, IUsable
{
    public bool TryUse(HitContext hitContext)
    {
        if (!hitContext.Target.TryGetComponent<PlayerController>(out PlayerController controller))
            return false;

        Revive(controller);

        Debug.Log($"[ReviveSyringe] {hitContext.Target.name} 부활");
        return true;
    }

    private void Revive(PlayerController targetController)
    {

        targetController.TryRevive();

    }
}
