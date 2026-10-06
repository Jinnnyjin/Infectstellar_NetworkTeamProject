/// <summary>
/// [확정 아님] 플레이어가 레이캐스트로 바라본 대상과의 상호작용. 현재 구현 대상은 잡을 수 있는 아이템
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 화면 표시용 정보 (가격 및 아이템명)
    /// </summary>
    public InteractableInfo GetInfo();

    /// <summary>
    /// "놓임" 상태일때만, (타 플레이어가 들고있다거나, 던져지고있을때는 false)
    /// </summary>
    public bool CanGrab();

    /// <summary>
    /// 로컬플레이어 IsMine에서만, CanGrab으로 미리 확인
    /// </summary>
    public void Grab();
}
