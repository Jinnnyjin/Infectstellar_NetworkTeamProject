using UnityEngine;

/// <summary>
/// 아이템 물리 담당: Kinematic, 콜라이더, 충돌 검사 모드
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ItemPhysics : MonoBehaviour
{
    private Rigidbody rb;
    private Collider[] colliders;

    /// <summary>
    /// 최하단 오프셋 계산에서 읽기 전용으로 사용
    /// </summary>
    public Collider[] Colliders => colliders;

    [Header("정지판정 [임시] 조정예정")]
    [SerializeField] private float stopLinearSpeed = 0.05f; // 이것보다 느리게 움직이면 멈춤
    [SerializeField] private float stopAngularSpeed = 0.2f; // 이것보다 느리게 회전하면 멈춤
    

    /// <summary>
    /// Item.Awake에서 호출.
    /// Awake 실행 순서가 보장되지 않아 Item이 직접 초기화 시점을 정함
    /// 켜진 콜라이더가 없으면 false
    /// </summary>
    public bool Init(float mass)
    {
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();

        if (!HasEnabledCollider())
            return false;

        rb.mass = mass;
        return true;
    }

    /// <summary>
    /// 켜기: 물리 시뮬레이션 + 터널링 방지 충돌 검사
    /// 끄기: Kinematic + 기본 충돌검사
    /// </summary>
    public void SetPhysics(bool isOn)
    {
        if(isOn)
        {
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = true;
        }
    }

    /// <summary>
    /// 물리가 켜진 이후 호출필요
    /// Kinematic이면 무시
    /// </summary>
    public void Launch(Vector3 force)
    {
        rb.AddForce(force,ForceMode.Impulse);
    }

    /// <summary>
    /// 이동, 회전 모두 기준보다 작은지 확인
    /// </summary>
    public bool IsBelowStopThreshold()
    {
        // 이동속도 크기의 제곱
        // magnitude 내부에서 제곱근 계산이 필요
        bool isLinearStopped = rb.linearVelocity.sqrMagnitude < stopLinearSpeed * stopLinearSpeed;
        bool isAngularStopped = rb.angularVelocity.sqrMagnitude < stopAngularSpeed * stopAngularSpeed;

        return isLinearStopped && isAngularStopped;
    }

    /// <summary>
    /// 자식콜라이더 전체 켜기/끄기 (콜라이더는 루트가 아닌 Model)
    /// </summary>
    public void SetColliders(bool isOn)
    {
        foreach(Collider col in colliders)
        {
            col.enabled = isOn;
        }
    }


    private bool HasEnabledCollider()
    {
        foreach(Collider col in colliders)
        {
            if(col.enabled) return true;
        }

        return false;
    }
}
