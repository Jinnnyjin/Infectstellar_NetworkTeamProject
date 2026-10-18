using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityHFSM;

/// <summary>공통 FSM으로 배회·추적·공격·수색·피격·사망을 처리합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(DamageSource))]
public sealed class MonsterController : MonoBehaviour
{
    private const float c_detectionInterval = 0.2f;
    [SerializeField] private MonsterSettings settings;
    [SerializeField, Tooltip("비활성 BoxCollider의 위치·회전·크기를 공격 범위로 사용")] private BoxCollider attackArea;
    [SerializeField, Tooltip("루트의 피격용 몸체 충돌체")] private Collider bodyCollider;
    [SerializeField, Tooltip("플레이어 감지 및 타격 대상 레이어")] private LayerMask playerLayers = 1 << 3;
    [SerializeField, Tooltip("시야를 가리는 환경 레이어. Player·Item은 제외")] private LayerMask obstacleLayers = 1;

    private readonly StateMachine<MonsterState> machine = new StateMachine<MonsterState>();
    private readonly Dictionary<Collider, Health> playerCache = new Dictionary<Collider, Health>();
    private readonly Dictionary<Health, Transform> transformCache = new Dictionary<Health, Transform>();
    private readonly HashSet<Health> hitPlayers = new HashSet<Health>();
    private NavMeshAgent agent;
    private Health health;
    private DamageSource damageSource;
    private Transform cachedTransform;
    private Transform attackTransform;
    private MonsterRestrictedZone[] restrictedZones;
    private Health target;
    private Transform targetTransform;
    private Vector3 lastSeenPosition;
    private bool hasLastSeenPosition;
    private bool isInitialized;
    private bool isWaiting;
    private bool hasStruck;
    private float nextDetection;
    private float stateStarted;
    private float waitUntil;
    private float stunnedUntil;

    /// <summary>현재 실행 중인 행동입니다.</summary>
    public MonsterState CurrentState => machine.ActiveStateName;

    /// <summary>필수 참조를 확보하고 설정된 최대 체력을 적용합니다.</summary>
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        damageSource = GetComponent<DamageSource>();
        cachedTransform = transform;
        if (settings == null || attackArea == null || bodyCollider == null || settings.MaxHealth <= 0f)
        {
#if UNITY_EDITOR
            Debug.LogError("[MonsterController] Settings·AttackArea·BodyCollider 연결을 확인하세요.", this);
#endif
            enabled = false;
            return;
        }

        attackTransform = attackArea.transform;
        attackArea.enabled = false;
        health.Initialize(settings.MaxHealth);
        damageSource.Init(settings.Damage);
        health.D_DamageApplied += HandleDamaged;
        health.D_Died += HandleDied;
    }

    /// <summary>씬의 금지 영역을 캐싱하고 유효한 NavMesh 위에서 FSM을 시작합니다.</summary>
    private void Start()
    {
        restrictedZones = FindObjectsByType<MonsterRestrictedZone>(FindObjectsSortMode.None);
        if (!agent.isOnNavMesh || IsRestricted(cachedTransform.position))
        {
#if UNITY_EDITOR
            Debug.LogError("[MonsterController] 허용된 NavMesh 위에 몬스터를 배치하세요.", this);
#endif
            enabled = false;
            return;
        }

        foreach (MonsterState state in System.Enum.GetValues(typeof(MonsterState)))
        {
            MonsterState captured = state;
            machine.AddState(captured, onEnter: _ => EnterState(captured), onLogic: _ => TickState(captured));
        }
        machine.SetStartState(health.HP > 0f ? MonsterState.Roam : MonsterState.Dead);
        isInitialized = true;
        machine.Init();
    }

    /// <summary>일정 간격으로 감지하고 현재 행동을 갱신합니다.</summary>
    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }
        if (CurrentState != MonsterState.Dead && Time.time >= nextDetection)
        {
            nextDetection = Time.time + c_detectionInterval;
            DetectPlayers();
        }
        machine.OnLogic();
    }

    /// <summary>재활성 시 미실행 공격을 재개하지 않고 행동을 다시 결정합니다.</summary>
    private void OnEnable()
    {
        if (isInitialized && CurrentState != MonsterState.Dead)
        {
            agent.isStopped = CurrentState == MonsterState.Stunned;
            agent.updateRotation = !agent.isStopped;
            if (CurrentState == MonsterState.Attack)
            {
                ChangeState(MonsterState.Chase);
            }
            DetectPlayers();
            if (CurrentState != MonsterState.Stunned)
            {
                DecideNextAction();
            }
        }
    }

    /// <summary>중단 시 이동을 멈추고 미실행 타격을 폐기합니다.</summary>
    private void OnDisable()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        hasStruck = true;
    }

    /// <summary>파괴 시 FSM과 체력 알림 구독을 정리합니다.</summary>
    private void OnDestroy()
    {
        if (isInitialized)
        {
            machine.OnExit();
        }
        if (health != null)
        {
            health.D_DamageApplied -= HandleDamaged;
            health.D_Died -= HandleDied;
        }
    }

    /// <summary>활성 상태만 전환하며 사망 후 행동 복귀를 차단합니다.</summary>
    private void ChangeState(MonsterState state)
    {
        if (CurrentState != MonsterState.Dead && CurrentState != state)
        {
            machine.RequestStateChange(state, true);
        }
    }

    /// <summary>행동 진입 시 경로·회전·시간을 설정합니다.</summary>
    private void EnterState(MonsterState state)
    {
        stateStarted = Time.time;
        isWaiting = false;
        agent.isStopped = state == MonsterState.Attack || state == MonsterState.Stunned || state == MonsterState.Dead;
        agent.updateRotation = !agent.isStopped;
        switch (state)
        {
            case MonsterState.Roam:
                hasLastSeenPosition = false;
                agent.speed = settings.RoamSpeed;
                ChooseRoamDestination();
                break;
            case MonsterState.Chase:
                agent.speed = settings.ChaseSpeed;
                MoveTo(lastSeenPosition);
                break;
            case MonsterState.Search:
                agent.speed = settings.ChaseSpeed;
                MoveTo(lastSeenPosition);
                break;
            case MonsterState.Attack:
                hasStruck = false;
                if (targetTransform != null)
                {
                    Vector3 facing = targetTransform.position - cachedTransform.position;
                    facing.y = 0f;
                    if (facing.sqrMagnitude > 0.0001f)
                    {
                        cachedTransform.rotation = Quaternion.LookRotation(facing);
                    }
                }
                break;
            case MonsterState.Dead:
                bodyCollider.enabled = false;
                agent.enabled = false;
                Destroy(gameObject, settings.CorpseDuration);
                break;
        }
    }

    /// <summary>선택된 상태의 행동과 완료 조건을 처리합니다.</summary>
    private void TickState(MonsterState state)
    {
        switch (state)
        {
            case MonsterState.Roam:
                if (target != null)
                {
                    ChangeState(MonsterState.Chase);
                }
                else if (WaitAtDestination(settings.RoamWait))
                {
                    ChooseRoamDestination();
                }
                break;
            case MonsterState.Chase:
                if (target == null)
                {
                    DecideNextAction();
                }
                break;
            case MonsterState.Search:
                if (target != null || WaitAtDestination(settings.SearchWait))
                {
                    hasLastSeenPosition = target != null;
                    DecideNextAction();
                }
                break;
            case MonsterState.Attack:
                if (!hasStruck && Time.time >= stateStarted + settings.AttackWindup)
                {
                    hasStruck = true;
                    Strike();
                }
                if (Time.time >= stateStarted + settings.AttackWindup + settings.AttackRecovery)
                {
                    DecideNextAction();
                }
                break;
            case MonsterState.Stunned:
                if (Time.time >= stunnedUntil)
                {
                    DetectPlayers();
                    DecideNextAction();
                }
                break;
        }
    }

    /// <summary>대상 또는 마지막 목격 정보에 맞춰 다음 행동을 정합니다.</summary>
    private void DecideNextAction()
    {
        ChangeState(target != null ? MonsterState.Chase : hasLastSeenPosition ? MonsterState.Search : MonsterState.Roam);
    }

    /// <summary>감지 가능한 기존·신규 후보 중 가장 가까운 생존 플레이어를 고릅니다.</summary>
    private void DetectPlayers()
    {
        Health previous = target;
        Health nearest = null;
        Transform nearestTransform = null;
        float nearestDistance = float.PositiveInfinity;
        bool previousUnavailable = !ReferenceEquals(previous, null) && (previous == null || !previous.isActiveAndEnabled || previous.HP <= 0f || targetTransform == null || IsRestricted(targetTransform.position));
        Collider[] candidates = Physics.OverlapSphere(cachedTransform.position, settings.ChaseRange, playerLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider candidate in candidates)
        {
            Health player = GetPlayer(candidate);
            if (!IsValidPlayer(player))
            {
                continue;
            }
            Transform playerTransform = transformCache[player];
            Vector3 difference = playerTransform.position - cachedTransform.position;
            float distance = difference.sqrMagnitude;
            float range = player == previous ? settings.ChaseRange : settings.DetectionRange;
            if (distance > range * range || distance >= nearestDistance)
            {
                continue;
            }
            difference.y = 0f;
            if (Vector3.Angle(cachedTransform.forward, difference) > settings.ViewAngle * 0.5f)
            {
                continue;
            }
            Vector3 eye = cachedTransform.position + Vector3.up * (agent.height * 0.75f);
            Vector3 endpoint = candidate.bounds.center;
            if (Physics.Linecast(eye, endpoint, obstacleLayers, QueryTriggerInteraction.Ignore))
            {
                continue;
            }
            nearest = player;
            nearestTransform = playerTransform;
            nearestDistance = distance;
        }
        target = nearest;
        targetTransform = nearestTransform;
        if (target != null)
        {
            lastSeenPosition = targetTransform.position;
            hasLastSeenPosition = true;
            if (CurrentState == MonsterState.Chase)
            {
                MoveTo(lastSeenPosition);
                if (TargetInAttackArea())
                {
                    ChangeState(MonsterState.Attack);
                }
            }
        }
        else if (previousUnavailable)
        {
            hasLastSeenPosition = false;
        }
    }

    /// <summary>플레이어 충돌체의 체력과 Transform을 처음에만 확보합니다.</summary>
    private Health GetPlayer(Collider candidate)
    {
        if (!playerCache.TryGetValue(candidate, out Health player))
        {
            player = candidate.GetComponentInParent<Health>();
            playerCache.Add(candidate, player);
            if (player != null && !transformCache.ContainsKey(player))
            {
                transformCache.Add(player, player.transform);
            }
        }
        return player;
    }

    /// <summary>생존·활성 상태와 금지 영역을 확인합니다.</summary>
    private bool IsValidPlayer(Health player)
    {
        return player != null && player.isActiveAndEnabled && player.HP > 0f && !IsRestricted(transformCache[player].position);
    }

    /// <summary>공유된 금지 영역 안인지 확인합니다.</summary>
    private bool IsRestricted(Vector3 position)
    {
        foreach (MonsterRestrictedZone zone in restrictedZones)
        {
            if (zone != null && zone.Contains(position))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>설정용 박스를 월드 공간 공격 범위로 조회합니다.</summary>
    private Collider[] QueryAttackArea()
    {
        Vector3 scale = attackTransform.lossyScale;
        Vector3 size = Vector3.Scale(attackArea.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return Physics.OverlapBox(attackTransform.TransformPoint(attackArea.center), size * 0.5f, attackTransform.rotation, playerLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>현재 타깃이 공격 박스에 들어왔는지 확인합니다.</summary>
    private bool TargetInAttackArea()
    {
        foreach (Collider candidate in QueryAttackArea())
        {
            if (GetPlayer(candidate) == target && IsValidPlayer(target))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>박스 내 유효한 플레이어마다 한 번만 피해를 줍니다.</summary>
    private void Strike()
    {
        hitPlayers.Clear();
        foreach (Collider candidate in QueryAttackArea())
        {
            Health player = GetPlayer(candidate);
            if (IsValidPlayer(player) && hitPlayers.Add(player))
            {
                damageSource.DealDamage(player);
            }
        }
    }

    /// <summary>허용된 NavMesh 목적지로 이동하고 잘못된 경로는 대기 처리합니다.</summary>
    private void MoveTo(Vector3 position)
    {
        isWaiting = false;
        if (!agent.SetDestination(position))
        {
            agent.ResetPath();
        }
    }

    /// <summary>도착 가능한 경로 끝에서 정지한 뒤 지정한 시간을 기다립니다.</summary>
    private bool WaitAtDestination(float duration)
    {
        if (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.05f))
        {
            return false;
        }
        if (!isWaiting)
        {
            isWaiting = true;
            waitUntil = Time.time + duration;
        }
        return Time.time >= waitUntil;
    }

    /// <summary>현재 위치 반경 안의 연결된 배회 목적지를 고릅니다.</summary>
    private void ChooseRoamDestination()
    {
        Vector2 offset = Random.insideUnitCircle * settings.RoamRadius;
        Vector3 destination = cachedTransform.position + new Vector3(offset.x, 0f, offset.y);
        agent.ResetPath();
        isWaiting = false;
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 1f, agent.areaMask) && !IsRestricted(hit.position) && Vector3.Distance(hit.position, cachedTransform.position) <= settings.RoamRadius)
        {
            MoveTo(hit.position);
        }
    }

    /// <summary>사망을 덮지 않고 마지막 피격부터 경직을 다시 계산합니다.</summary>
    private void HandleDamaged(DamageInfo damageInfo)
    {
        if (!isInitialized || !isActiveAndEnabled || health.HP <= 0f)
        {
            return;
        }
        stunnedUntil = Time.time + settings.StunDuration;
        ChangeState(MonsterState.Stunned);
    }

    /// <summary>체력 0을 모든 생존 행동보다 우선 적용합니다.</summary>
    private void HandleDied(DamageInfo damageInfo)
    {
        if (isInitialized)
        {
            ChangeState(MonsterState.Dead);
        }
    }
}
