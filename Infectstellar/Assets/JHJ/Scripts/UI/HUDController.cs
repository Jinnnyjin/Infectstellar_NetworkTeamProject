using UnityEngine;

public class HUDController : MonoBehaviour
{
    [SerializeField] private HeldItemView heldItemView;
    [SerializeField] private InteractInfoView interactInfoView;

    public void InitialSetting(GameObject player)
    {
        if(player == null)
        {
            Debug.LogError("[HUDController] player가 null", this);
            return;
        }

        PlayerGrabber grabber = player.GetComponent<PlayerGrabber>();
        PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();

        if (grabber == null)
            Debug.LogError($"[HUDController] {player.name}의 PlayerGrabber 없음", this);
        if (interactor == null)
            Debug.LogError($"[HUDController] {player.name}의 PlayerInteractor 없음", this);

        // 들고있는 아이템 HUD
        if (heldItemView == null)
            Debug.LogError("[HUDController] heldItemView 미연결", this);
        else if (grabber != null)
            heldItemView.InitialSetting(grabber);

        // 상호작용HUD
        if (interactInfoView == null)
            Debug.LogError("[HUDController] interactInfoView 미연결", this);
        else if (grabber != null && interactor != null)
            interactInfoView.InitialSetting(interactor, grabber);
    }
}
