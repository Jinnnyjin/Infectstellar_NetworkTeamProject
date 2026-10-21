using ENZEUN.Runtime;
using UnityEngine;

public class DoorToggleButton : MonoBehaviour, IInteractObject
{

    private bool isDoorOpen = false;
    public string PromptText => isDoorOpen ? InteractPromptSO.Instance.DoorClose : InteractPromptSO.Instance.DoorOpen;

    // GameManager 연결시 아래 코드로 수정예정
    // public string PromptText => GameManager.Instance.IsDoorOpen ? InteractPromptSO.Instance.DoorClose : InteractPromptSO.Instance.DoorOpen;


    // 조건 : 게임매니저 O, 게임중
    public bool CanInteract => GameManager.Instance != null;

    public void Interact()
    {
        if (!CanInteract)
        {
            Debug.LogWarning("[EscapeButton] 탈출버튼 상호작용 불가", this);
            return;
        }

        if (!isSubscribed)
            SubscribeGM();

        gameManager.RequestToggleDoor();

        Debug.Log("[EscapeButton] 탈출 버튼 상호작용 성공", this);

    }

    public void OpenDoor(bool open)
    {
        isDoorOpen = open;
    }


    private GameManager gameManager;
    private bool isSubscribed = false;

    private void SubscribeGM()
    {
        if (isSubscribed)
            return;

        gameManager = GameManager.Instance;

        gameManager.OnDoorButtonClicked += OpenDoor;

        isSubscribed = true;
    }

    public void OnDisable()
    {
        gameManager.OnDoorButtonClicked -= OpenDoor;

    }
}
