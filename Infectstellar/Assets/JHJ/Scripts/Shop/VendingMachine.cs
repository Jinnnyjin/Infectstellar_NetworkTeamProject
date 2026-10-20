using UnityEngine;

/// <summary>
/// 실제 구매는 Shop.cs
/// 상점 단말기. 바라보고 E키로 상호작용
/// </summary>
public class VendingMachine : MonoBehaviour, IInteractObject
{
    [SerializeField] private string promptText = "[E] 상점 열기";

    public string PromptText => promptText;
    public bool CanInteract => true;

    public void Interact()
    {
        // [임시] 상점 UI 작업 전, 상호작용 확인용
        Debug.Log($"[ShopTerminal] 상호작용: {gameObject.name}", this);
    }


}
