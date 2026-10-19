using UnityEngine;

/// <summary>모든 몬스터가 공유하는 행동의 종류별 수치입니다.</summary>
[CreateAssetMenu(menuName = "Monster/Settings")]
public sealed class MonsterSettings : ScriptableObject
{
    [SerializeField, Min(0.01f)] private float maxHealth = 100f;
    [SerializeField, Min(0.01f)] private float damage = 10f;
    [SerializeField, Min(0.01f)] private float roamSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float chaseSpeed = 3.5f;
    [SerializeField, Min(0.01f), Tooltip("새 대상 발견 거리(m)")] private float detectionRange = 10f;
    [SerializeField, Min(0.01f), Tooltip("기존 대상 유지 거리(m). 감지 거리 이상으로 설정")] private float chaseRange = 20f;
    [SerializeField, Range(1f, 360f), Tooltip("수평 전체 시야각(도)")] private float viewAngle = 120f;
    [SerializeField, Min(0.01f)] private float roamRadius = 5f;
    [SerializeField, Min(0f)] private float roamWait = 2f;
    [SerializeField, Min(0f)] private float searchWait = 3f;
    [SerializeField, Min(0f)] private float attackWindup = 0.5f;
    [SerializeField, Min(0.01f)] private float attackRecovery = 1f;
    [SerializeField, Min(0f)] private float stunDuration = 0.5f;
    [SerializeField, Min(0f)] private float corpseDuration = 3f;

    /// <summary>초기 최대 체력입니다.</summary>
    public float MaxHealth => maxHealth;
    /// <summary>한 번의 타격 피해량입니다.</summary>
    public float Damage => damage;
    /// <summary>배회 속도(m/s)입니다.</summary>
    public float RoamSpeed => roamSpeed;
    /// <summary>추적과 수색 이동 속도(m/s)입니다.</summary>
    public float ChaseSpeed => chaseSpeed;
    /// <summary>새 대상 감지 거리(m)입니다.</summary>
    public float DetectionRange => detectionRange;
    /// <summary>기존 대상 유지 거리(m)입니다.</summary>
    public float ChaseRange => Mathf.Max(detectionRange, chaseRange);
    /// <summary>수평 전체 시야각(도)입니다.</summary>
    public float ViewAngle => viewAngle;
    /// <summary>현재 위치 기준 배회 목적지 반경(m)입니다.</summary>
    public float RoamRadius => roamRadius;
    /// <summary>배회 도착 후 대기 시간(초)입니다.</summary>
    public float RoamWait => roamWait;
    /// <summary>마지막 목격 지점 도착 후 대기 시간(초)입니다.</summary>
    public float SearchWait => searchWait;
    /// <summary>타격 전 준비 시간(초)입니다.</summary>
    public float AttackWindup => attackWindup;
    /// <summary>타격 후 다음 행동까지 대기 시간(초)입니다.</summary>
    public float AttackRecovery => attackRecovery;
    /// <summary>마지막 피격부터 적용할 경직 시간(초)입니다.</summary>
    public float StunDuration => stunDuration;
    /// <summary>사망 후 제거까지 대기 시간(초)입니다.</summary>
    public float CorpseDuration => corpseDuration;
}
