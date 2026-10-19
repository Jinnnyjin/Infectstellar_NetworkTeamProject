using UnityEngine;

public interface IUsable
{
    /// <summary>
    /// 적중 시 사용 시도.
    /// true = 사용 성공 (아이템 사라짐, 기본 데미지 생략)
    /// false = 사용 실패 (일반 아이템처럼 데미지 적용, 다시 주울 수 있음)
    /// </summary>
    public bool TryUse(HitContext hitContext);
}
