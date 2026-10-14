// [임시] 아이템 적중 테스트용 더미 표적 (네트워크 X)
// 플레이어 쪽 Damage 처리 합치기 전까지 사용, 통합 시 삭제 예정
using UnityEngine;

/// <summary>
/// [임시] ITempDamageable 테스트용 표적
/// actorNumber로 몇 번 플레이어인지 흉내 내고, 본인 공격은 무시
/// </summary>
public class DummyTarget : MonoBehaviour, ITempDamageable // [임시]
{
    [Tooltip("이 표적이 흉내 낼 플레이어 번호")]
    [SerializeField] private int actorNumber = 2;
    [SerializeField] private float maxHp = 100f;

    private float currentHp;


    private void Awake()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(TempDamageInfo info)
    {
        if (info.AttackerActorNumber == actorNumber)
        {
            Debug.Log($"[DummyTarget] {gameObject.name}: 본인이라 무시 (공격자 {info.AttackerActorNumber})", this);
            return;
        }

        currentHp -= info.Damage;
        Debug.Log($"[DummyTarget] {gameObject.name}: 공격자 {info.AttackerActorNumber}, 데미지 {info.Damage}, 남은 체력 {currentHp}", this);

        if (currentHp <= 0f)
        {
            Debug.Log($"[DummyTarget] {gameObject.name}: 쓰러짐", this);
        }
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 플레이 전엔 Awake 전이라 maxHp 표시
        float hp = Application.isPlaying ? currentHp : maxHp;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"Actor {actorNumber}\nHP {hp}/{maxHp}");
#endif
    }
}
