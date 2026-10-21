using UnityEngine;

/// <summary>
/// 아이템이 아닌 상호작용 대상( 상점, 문 등..)
/// 바라보고 상호작용키(E) 누르면 호출
/// </summary>
public interface IInteractObject
{
    public string PromptText { get; }   // "[E] 상점 열기"
    public string Description { get; }    
    public bool CanInteract { get; }
    public void Interact();
}
