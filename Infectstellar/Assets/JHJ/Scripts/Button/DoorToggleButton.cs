using ENZEUN.Runtime;
using UnityEngine;

public class DoorToggleButton : MonoBehaviour, IInteractObject
{

    [SerializeField] private string openText = "[E] 문 열기";
    [SerializeField] private string closeText = "[E] 문 닫기";

    // 게임매니저에서 IsDoorOpen 값 가지고 있다면 GameManager.IsDoorOpen으로 수정 예정
    private bool isDoorOpen = false;
    public string PromptText => isDoorOpen ? closeText : openText;



    // 게임 시작상태에서만 되도록 하려면 이 조건이 맞는지 확인 필요
    public bool CanInteract => GameManager.Instance != null; // && GameManager.Instance.currentGameState == GameManager.GameStateFSM.GameStarted;


    public void Interact()
    {
        if (!CanInteract)
        {
            Debug.LogWarning("[DoorTogleButton] 문 상호작용 불가");
            return;
        }

        // 문 열림 닫힘 상태에 따라 문 열리거나 닫힘

        Debug.Log("[DoorToggleButton]문 상호작용 성공");

    }
}
