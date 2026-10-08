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
    /// 플레이어가 Grab 처리 중 호출
    /// 아이템은 상태전환
    /// </summary>
    public void OnGrab();
}
