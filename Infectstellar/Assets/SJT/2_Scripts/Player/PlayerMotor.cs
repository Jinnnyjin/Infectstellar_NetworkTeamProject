using UnityEngine;

/// <summary>
/// HFSM의 명령으로 CharacterController 이동, 중력과 충돌 자세를 처리합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMotor : MonoBehaviour
{
    private const float c_groundStickSpeed = 2f;
    private const float c_minProbeDistance = 0.01f;
    private const float c_queryInsetRatio = 0.1f;
    private const int c_queryCapacity = 32;

    private readonly Collider[] overlaps = new Collider[c_queryCapacity];
    private readonly RaycastHit[] groundHits = new RaycastHit[c_queryCapacity];
    private CharacterController characterController;
    private Transform body;
    private PlayerMovementSettings settings;
    private float standingHeight;
    private Vector3 standingCenter;
    private float standingStepOffset;
    private float worldScale;
    private float verticalVelocity;
    private int collisionMask;
    private bool isGrounded;
    private bool isCrouching;

    /// <summary>
    /// 접지 판정 결과입니다. 상승 중에는 false입니다.
    /// </summary>
    internal bool IsGrounded => isGrounded;

    /// <summary>
    /// 위쪽이 양수인 현재 수직 속도입니다.
    /// </summary>
    internal float VerticalVelocity => verticalVelocity;

    /// <summary>
    /// 실제 충돌 캡슐이 웅크린 자세인지 반환합니다.
    /// </summary>
    internal bool IsCrouching => isCrouching;

    /// <summary>
    /// 필요한 컴포넌트와 설정이 살아 있고 활성 상태인지 반환합니다.
    /// </summary>
    internal bool CanSimulate => isActiveAndEnabled && characterController != null && characterController.enabled && settings != null;

    /// <summary>
    /// 같은 플레이어의 캡슐과 설정을 캐싱하고 원래 선 자세를 보관합니다.
    /// </summary>
    internal bool Initialize(PlayerMovementSettings settings)
    {
        if (this.settings != null)
        {
            return this.settings == settings;
        }

        characterController = GetComponent<CharacterController>();
        body = transform;
        Vector3 scale = body.lossyScale;
        if (characterController == null || settings == null || !settings.IsValid())
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerMotor] CharacterController와 유효한 이동 설정이 필요합니다.", this);
#endif
            return false;
        }

        if (scale.x <= 0f || !Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.y, scale.z) || Vector3.Dot(body.up, Vector3.up) < 0.999f)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerMotor] 양수의 균일 스케일과 수직으로 선 플레이어가 필요합니다.", this);
#endif
            return false;
        }

        standingHeight = characterController.height;
        standingCenter = characterController.center;
        standingStepOffset = characterController.stepOffset;
        worldScale = scale.y;
        if (standingHeight * settings.CrouchHeightRatio < characterController.radius * 2f)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerMotor] 웅크린 캡슐 높이가 지름보다 작습니다. 캡슐 또는 이동 설정을 확인하세요.", this);
#endif
            return false;
        }

        for (int layer = 0; layer < 32; layer++)
        {
            if (!Physics.GetIgnoreLayerCollision(gameObject.layer, layer))
            {
                collisionMask |= 1 << layer;
            }
        }

        this.settings = settings;
        RefreshGrounded();
        return true;
    }

    /// <summary>
    /// 이동 전에도 현재 위치에서 접지를 확인해 초기 배치와 부활을 반영합니다.
    /// </summary>
    internal void RefreshGrounded()
    {
        isGrounded = verticalVelocity <= 0f && ProbeGround();
    }

    /// <summary>
    /// 선 자세의 캡슐을 막는 충돌체가 없는지 확인합니다. 자신의 충돌체와 Trigger는 제외합니다.
    /// </summary>
    internal bool CanStand()
    {
        if (!isCrouching)
        {
            return true;
        }

        GetCapsule(standingHeight, standingCenter, out _, out Vector3 top, out float radius);
        GetCapsule(characterController.height, characterController.center, out _, out Vector3 bottom, out _);
        // 현재 머리부터 선 자세의 머리까지 전체 반지름으로 검사해 바닥을 제외하고 천장 여유를 줄이지 않습니다.
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            if (IsBlocking(overlaps[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 발 위치를 유지하며 캡슐 높이를 바꾸고, 공간이 없으면 일어서기를 거부합니다.
    /// </summary>
    internal void SetCrouching(bool crouch)
    {
        if (isCrouching == crouch || (!crouch && !CanStand()))
        {
            return;
        }

        float height = crouch ? standingHeight * settings.CrouchHeightRatio : standingHeight;
        characterController.height = height;
        characterController.center = standingCenter - Vector3.up * ((standingHeight - height) * 0.5f);
        isCrouching = crouch;
    }

    /// <summary>
    /// 한 프레임의 명령을 실행합니다. 중력 적분과 Move는 이 메서드에서 한 번만 수행합니다.
    /// </summary>
    internal void Simulate(PlayerMovementCommand command, float deltaTime)
    {
        SetCrouching(command.Crouch);
        if (command.Jump && isGrounded && !isCrouching)
        {
            verticalVelocity = Mathf.Sqrt(2f * settings.Gravity * settings.JumpHeight);
            isGrounded = false;
        }

        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 horizontalVelocity = (right * command.Move.x + forward * command.Move.y) * command.Speed;
        float verticalDistance;
        if (isGrounded)
        {
            verticalVelocity = 0f;
            verticalDistance = -c_groundStickSpeed * deltaTime;
        }
        else
        {
            verticalDistance = verticalVelocity * deltaTime - 0.5f * settings.Gravity * deltaTime * deltaTime;
            verticalVelocity -= settings.Gravity * deltaTime;
        }

        // Step Offset은 미터 단위이므로 로컬 캡슐 높이와 비교하기 전에 월드 길이로 변환합니다.
        float maxStepOffset = Mathf.Max((characterController.height - characterController.radius * 2f) * worldScale, 0f);
        characterController.stepOffset = isGrounded ? Mathf.Min(standingStepOffset, maxStepOffset) : 0f;
        CollisionFlags collisions = characterController.Move(horizontalVelocity * deltaTime + Vector3.up * verticalDistance);
        if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        isGrounded = verticalVelocity <= 0f && ((collisions & CollisionFlags.Below) != 0 || ProbeGround());
        if (isGrounded)
        {
            verticalVelocity = 0f;
        }
    }

    /// <summary>
    /// 캐릭터의 발밑에서 접지 가능한 기울기의 바닥을 찾습니다.
    /// </summary>
    private bool ProbeGround()
    {
        GetCapsule(characterController.height, characterController.center, out Vector3 bottom, out _, out float radius);
        float probeDistance = Mathf.Max(radius * c_queryInsetRatio, c_minProbeDistance);
        float queryRadius = Mathf.Max(radius - probeDistance, radius * 0.5f);
        int count = Physics.SphereCastNonAlloc(bottom + Vector3.up * probeDistance, queryRadius, Vector3.down, groundHits, probeDistance * 2f + c_minProbeDistance, collisionMask, QueryTriggerInteraction.Ignore);
        float minGroundDot = Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (IsBlocking(hit.collider) && Vector3.Dot(hit.normal, Vector3.up) >= minGroundDot)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 로컬 캡슐을 월드의 구 중심 두 점과 반지름으로 변환합니다.
    /// </summary>
    private void GetCapsule(float height, Vector3 center, out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 worldCenter = body.TransformPoint(center);
        radius = characterController.radius * worldScale;
        float offset = Mathf.Max(height * worldScale * 0.5f - radius, 0f);
        bottom = worldCenter - Vector3.up * offset;
        top = worldCenter + Vector3.up * offset;
    }

    /// <summary>
    /// 자신의 계층이나 명시적으로 충돌을 무시하는 쌍을 판정에서 제외합니다.
    /// </summary>
    private bool IsBlocking(Collider other)
    {
        return other != null && !other.transform.IsChildOf(body) && !Physics.GetIgnoreCollision(characterController, other);
    }
}
