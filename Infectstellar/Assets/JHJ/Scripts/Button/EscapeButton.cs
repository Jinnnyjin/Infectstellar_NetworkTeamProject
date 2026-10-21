using UnityEngine;
using ENZEUN.Runtime;

public class EscapeButton : MonoBehaviour, IInteractObject
{
    private bool isDoorOpen = false;
    public string PromptText => isDoorOpen ? InteractPromptSO.Instance.EscapeBlocked : InteractPromptSO.Instance.Escape;

    // GameManager 연결시 아래 코드로 수정예정
    // public string PromptText => GameManager.Instance.IsDoorOpen ? InteractPromptSO.Instance.EscapeBlocked : InteractPromptSO.Instance.Escape;


    // 조건 : 게임매니저 O, 문이 닫혀있을 때, 게임중
    public bool CanInteract => GameManager.Instance != null && !GameManager.Instance.doorIsOpen;
    public bool doOnce = false;


    public void Interact()
    {
        if (doOnce)
        {
            Debug.LogWarning("[EscapeButton] 탈출버튼 상호작용 불가", this);
            return;
        }
        if (!CanInteract)
        {
            Debug.LogWarning("[EscapeButton] 탈출버튼 상호작용 불가", this);
            return;
        }

        GameManager.Instance.RequestEscape();

        Debug.Log("[EscapeButton] 탈출 버튼 상호작용 성공", this);

        doOnce = true;
    }

}
