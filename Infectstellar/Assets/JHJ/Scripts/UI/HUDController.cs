using UnityEngine;

public class HUDController : MonoBehaviour
{
    [SerializeField] private HeldItemView heldItemView;

    public void InitialSetting(GameObject player)
    {
        if(player == null)
        {
            Debug.LogError("[HUDController] player가 null", this);
            return;
        }

        PlayerGrabber grabber = player.GetComponent<PlayerGrabber>();
        if(grabber == null)
        {
            Debug.LogError($"[HUDController] {player.name}의 PlayerGrabber 비어있음", this);
            return;
        }

        if (heldItemView == null)
        {
            Debug.LogError("[HUDController] heldItemView 미연결", this);
            return;
        }

        heldItemView.InitialSetting(grabber);
    }
}
