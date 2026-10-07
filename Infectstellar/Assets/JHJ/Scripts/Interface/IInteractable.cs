/// <summary>
/// 플레이어가 레이캐스트로 바라본 대상과의 상호작용.
/// 현재 구현 대상은 아이템 데이터(SO)를 가진 대상만 구현
/// 잡을 수 없는 대상이 생기면 IGrabbable 분리를 검토 필요
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 아이템 종류별 고정 데이터 (읽기 전용)
    /// 무게 등에서 활용
    /// </summary>
    public ItemDataSO ItemData { get; }

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
    public void Grab(); // 소켓 매개변수 들어갈 예정 
}
