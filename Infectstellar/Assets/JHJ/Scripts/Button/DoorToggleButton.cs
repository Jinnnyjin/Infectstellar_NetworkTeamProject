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
            Debug.LogWarning("[DoorToggleButton] 문 상호작용 불가", this);
            return;
        }

        // 문 열림 닫힘 상태에 따라 문 열리거나 닫힘

        Debug.Log("[DoorToggleButton]문 상호작용 성공", this);

    }
}
