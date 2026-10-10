using UnityEngine;

/// <summary>
/// HFSM의 이동 명령을 받아 실제 몸의 이동과 충돌을 처리하는 학습용 참조본입니다.
/// _PlayerMotor의 계산과 실행 순서를 유지하며, 단계별 메서드로 흐름을 구분합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class _PlayerMotor : MonoBehaviour
{
    // 접지 중 바닥과의 접촉을 유지하기 위해 아래로 이동을 시도하는 속도입니다. 단위: m/s.
    private const float c_groundStickSpeed = 2f;
    // 바닥 검사 여유 거리의 최솟값이며, 검사 길이에도 추가합니다. 단위: m.
    private const float c_minProbeDistance = 0.01f;
    // 캡슐 반지름의 10%를 기준으로 검사 시작점과 검사 구의 여유를 정합니다.
    private const float c_queryInsetRatio = 0.1f;
    // NonAlloc 검사에서 재사용할 각 결과 배열의 크기입니다.
    private const int c_queryCapacity = 32;

    private readonly Collider[] overlaps = new Collider[c_queryCapacity];
    private readonly RaycastHit[] groundHits = new RaycastHit[c_queryCapacity];

    // 초기화할 때 저장하고 이후 이동과 검사에 재사용하는 참조입니다.
    private CharacterController characterController;
    private Transform body;
    private PlayerMovementSettings settings;

    // 웅크렸다가 일어설 때 복원할 원래 캡슐과 계단 설정입니다.
    private float standingHeight;
    private Vector3 standingCenter;
    private float standingStepOffset;
    // 로컬 캡슐 크기를 월드 길이로 바꾸는 균일 스케일입니다.
    private float worldScale;
    private int collisionMask;

    // 다음 프레임에도 유지하고, 상태 판단에 전달할 실제 몸의 정보입니다.
    private float verticalVelocity;
    private bool isGrounded;
    private bool isCrouching;

    /// <summary>
    /// 현재 접지 여부입니다. 위로 상승하는 동안에는 false입니다.
    /// </summary>
    internal bool IsGrounded => isGrounded;

    /// <summary>
    /// 현재 수직 속도입니다. 위쪽이 양수이며 단위는 m/s입니다.
    /// </summary>
    internal float VerticalVelocity => verticalVelocity;

    /// <summary>
    /// 실제 충돌 캡슐이 웅크린 자세인지 반환합니다.
    /// </summary>
    internal bool IsCrouching => isCrouching;

    /// <summary>
    /// Motor와 CharacterController가 활성 상태이며 초기화된 설정이 있는지 확인합니다.
    /// </summary>
    internal bool CanSimulate => isActiveAndEnabled
        && characterController != null
        && characterController.enabled
        && settings != null;

    /// <summary>
    /// 컴포넌트와 설정을 확인하고, 원래 캡슐 정보와 충돌 검사 대상을 저장합니다.
    /// 준비가 끝나면 현재 접지를 갱신하며, 같은 설정으로 다시 호출하면 재초기화하지 않습니다.
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

        // 1. 실제 몸을 움직일 컴포넌트를 확인합니다.
        if (characterController == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerMotor] CharacterController가 필요합니다.", this);
#endif
            return false;
        }

        // 2. 이동 설정이 존재하고 값이 유효한지 확인합니다.
        if (settings == null || !settings.IsValid())
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerMotor] 유효한 이동 설정이 필요합니다.", this);
#endif
            return false;
        }

        // 3. 캡슐 계산은 양수의 균일 스케일과 수직으로 선 몸을 기준으로 합니다.
        if (scale.x <= 0f
            || !Mathf.Approximately(scale.x, scale.y)
            || !Mathf.Approximately(scale.y, scale.z)
            || Vector3.Dot(body.up, Vector3.up) < 0.999f)
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

        // 4. 캡슐 높이는 양 끝 구의 지름보다 작아질 수 없습니다. 둘 다 로컬 길이입니다.
        float crouchingHeight = standingHeight * settings.CrouchHeightRatio;
        float capsuleDiameter = characterController.radius * 2f;
        if (crouchingHeight < capsuleDiameter)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerMotor] 웅크린 캡슐 높이가 지름보다 작습니다. 캡슐 또는 이동 설정을 확인하세요.", this);
#endif
            return false;
        }

        // 1 << layer는 해당 레이어의 비트입니다. |로 충돌 가능한 레이어를 마스크에 모읍니다.
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
    /// 충돌 검사에 필요한 아래쪽·위쪽 구의 월드 중심 위치와 월드 반지름을 out으로 전달합니다.
    /// bottom과 top은 발끝·머리끝 표면이 아니라, 캡슐 양 끝의 둥근 부분을 이루는 구의 중심입니다.
    /// 캡슐의 실제 크기나 위치는 바꾸지 않으며, 이 메서드에서 충돌 검사를 실행하지는 않습니다.
    /// </summary>
    /// <remarks>
    /// Unity의 OverlapCapsuleNonAlloc은 두 구의 중심과 반지름으로 가상의 캡슐 공간을 정해 겹치는 충돌체를 찾습니다.
    /// 여기서 계산한 값은 ProbeGround의 발밑 검사와 CanStand의 일어설 공간 검사에 사용합니다.
    /// height와 center에 선 자세의 값을 넣으면, 실제로 일어서기 전에도 그 자세의 검사 정보를 구할 수 있습니다.
    /// 몸이 수직으로 서 있고 worldScale이 양수의 균일 스케일이라는 전제로 계산합니다.
    /// </remarks>
    /// <param name="height">입력: 위아래 둥근 부분까지 포함한 캡슐 전체 높이입니다. 스케일 적용 전의 로컬 길이입니다.</param>
    /// <param name="center">입력: 플레이어의 Transform을 기준으로 한 캡슐 전체 중심의 로컬 위치입니다.</param>
    /// <param name="bottom">출력: 아래쪽 구의 월드 중심 위치입니다. 캡슐 아래쪽 끝 표면보다 반지름만큼 위에 있습니다.</param>
    /// <param name="top">출력: 위쪽 구의 월드 중심 위치입니다. 캡슐 위쪽 끝 표면보다 반지름만큼 아래에 있습니다.</param>
    /// <param name="radius">출력: CharacterController의 반지름에 크기 배율을 적용한 월드 반지름입니다. 위아래 구가 같은 값을 사용합니다.</param>
    private void GetCapsule(float height, Vector3 center, out Vector3 bottom, out Vector3 top, out float radius)
    {
        // worldCenter: 캡슐 전체 중심이 게임 세계의 어디에 있는지 나타내는 위치입니다.
        // TransformPoint는 로컬 위치인 center에 부모 관계와 몸의 위치·회전·스케일을 반영합니다.
        Vector3 worldCenter = body.TransformPoint(center);

        // radius: 위아래 구 중심에서 각 구 표면까지의 월드 거리입니다.
        // CharacterController는 캡슐 모양이며, 그 로컬 반지름에 배율 worldScale을 곱합니다.
        radius = characterController.radius * worldScale;

        // worldHalfHeight: 캡슐 전체 중심에서 머리끝 또는 발끝 표면까지의 월드 거리입니다.
        // 전체 높이에 스케일을 적용한 다음 절반으로 나눕니다.
        float worldHalfHeight = height * worldScale * 0.5f;

        // sphereCenterOffset: 캡슐 전체 중심에서 위쪽 또는 아래쪽 구 중심까지의 월드 거리입니다.
        // 구 중심은 끝 표면보다 반지름만큼 안쪽에 있으므로 뺍니다. 예: 중심→끝 1.5 - 반지름 0.5 = 중심→구 중심 1.
        // Max는 음수를 0으로 제한합니다. 높이가 지름과 같으면 거리도 0이 되어 두 구 중심이 같은 위치에 놓입니다.
        float sphereCenterOffset = Mathf.Max(worldHalfHeight - radius, 0f);

        // 세계의 위쪽 방향(Vector3.up)을 기준으로 전체 중심에서 같은 거리만큼 내려가거나 올라갑니다.
        // 이렇게 얻은 두 구 중심과 radius를 호출한 쪽에 전달하며, 실제 충돌 검사는 그쪽에서 수행합니다.
        bottom = worldCenter - Vector3.up * sphereCenterOffset;
        top = worldCenter + Vector3.up * sphereCenterOffset;
    }

    /// <summary>
    /// 검사된 충돌체가 실제로 몸을 막는 대상인지 확인합니다.
    /// 자기 몸과 자식 충돌체, 명시적으로 충돌을 무시한 쌍은 제외합니다.
    /// </summary>
    private bool IsBlocking(Collider other)
    {
        return other != null
            && !other.transform.IsChildOf(body)
            && !Physics.GetIgnoreCollision(characterController, other);
    }

    /// <summary>
    /// 발밑으로 구를 움직여 검사하고, 설 수 있는 기울기의 바닥이 있는지 반환합니다.
    /// 검사 결과는 groundHits에 저장하며, 접지 필드 자체는 변경하지 않습니다.
    /// </summary>
    private bool ProbeGround()
    {
        GetCapsule(characterController.height, characterController.center,
            out Vector3 bottomSphereCenter, out _, out float radius);

        // 검사 구를 조금 줄이고 시작점을 올려, 처음부터 바닥에 겹칠 가능성을 줄입니다.
        float probeDistance = Mathf.Max(radius * c_queryInsetRatio, c_minProbeDistance);
        float queryRadius = Mathf.Max(radius - probeDistance, radius * 0.5f);
        Vector3 castOrigin = bottomSphereCenter + Vector3.up * probeDistance;
        float castDistance = probeDistance * 2f + c_minProbeDistance;

        int count = Physics.SphereCastNonAlloc(
            castOrigin,
            queryRadius,
            Vector3.down,
            groundHits,
            castDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        // 바닥의 법선과 위쪽 방향의 내적을 비교해 slopeLimit 이내의 경사만 허용합니다.
        float minGroundDot = Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (!IsBlocking(hit.collider))
            {
                continue;
            }

            float groundDot = Vector3.Dot(hit.normal, Vector3.up);
            if (groundDot >= minGroundDot)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 이동 전 현재 위치의 접지를 검사해 isGrounded를 갱신합니다.
    /// 상승 중에는 검사하지 않고 false로 두어 점프 직후 바닥에 다시 붙지 않게 합니다.
    /// </summary>
    internal void RefreshGrounded()
    {
        isGrounded = verticalVelocity <= 0f && ProbeGround();
    }

    /// <summary>
    /// 웅크린 몸이 일어설 때 머리 위 공간을 막는 충돌체가 있는지 검사합니다.
    /// 실제 자세는 바꾸지 않으며, 이미 서 있다면 바로 true를 반환합니다.
    /// </summary>
    internal bool CanStand()
    {
        if (!isCrouching)
        {
            return true;
        }

        GetCapsule(standingHeight, standingCenter,
            out _, out Vector3 standingTopSphereCenter, out float radius);
        GetCapsule(characterController.height, characterController.center,
            out _, out Vector3 crouchingTopSphereCenter, out _);

        // 현재 머리부터 선 자세의 머리까지 원래 반지름으로 검사합니다.
        // 이 검사의 아래쪽 끝도 몸 전체의 발 위치가 아니라 현재 머리 쪽 구 중심입니다.
        int count = Physics.OverlapCapsuleNonAlloc(
            crouchingTopSphereCenter,
            standingTopSphereCenter,
            radius,
            overlaps,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        // 배열이 꽉 차면 저장하지 못한 장애물이 있을 수 있어 일어서기를 거부합니다.
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
    /// 발 위치를 유지하며 캡슐 높이·중심과 isCrouching을 변경합니다.
    /// 자세가 같거나 일어설 공간이 없으면 현재 자세를 유지합니다.
    /// </summary>
    internal void SetCrouching(bool crouch)
    {
        if (isCrouching == crouch)
        {
            return;
        }

        if (!crouch && !CanStand())
        {
            return;
        }

        float height = crouch ? standingHeight * settings.CrouchHeightRatio : standingHeight;
        characterController.height = height;

        // 줄어든 높이의 절반만큼 중심도 내려야 캡슐의 발 위치가 유지됩니다. 단위: 로컬 길이.
        float heightDifference = standingHeight - height;
        float centerOffset = heightDifference * 0.5f;
        characterController.center = standingCenter - Vector3.up * centerOffset;
        isCrouching = crouch;
    }

    /// <summary>
    /// 한 프레임의 명령을 자세 → 점프 → 이동 계산 → 실제 이동 → 충돌 반영 순서로 실행합니다.
    /// 중력 갱신과 CharacterController.Move는 이 흐름에서 각각 한 번만 수행합니다.
    /// </summary>
    internal void Simulate(PlayerMovementCommand command, float deltaTime)
    {
        SetCrouching(command.Crouch);
        TryStartJump(command.Jump);

        Vector3 horizontalVelocity = CalculateHorizontalVelocity(command.Move, command.Speed);
        float verticalDistance = CalculateVerticalDistance(deltaTime);

        UpdateStepOffset();
        Vector3 movement = horizontalVelocity * deltaTime + Vector3.up * verticalDistance;
        CollisionFlags collisions = characterController.Move(movement);

        ApplyCollisionResult(collisions);
    }

    /// <summary>
    /// 점프 명령이 있고 접지 중이며 실제로 서 있을 때만 점프를 시작합니다.
    /// 시작하면 verticalVelocity를 설정하고 isGrounded를 즉시 false로 변경합니다.
    /// </summary>
    private void TryStartJump(bool jumpRequested)
    {
        if (!jumpRequested || !isGrounded || isCrouching)
        {
            return;
        }

        // 원하는 높이 h에 도달할 초기 속도는 sqrt(2 * g * h)입니다. 결과 단위: m/s.
        float jumpSpeedSquared = 2f * settings.Gravity * settings.JumpHeight;
        verticalVelocity = Mathf.Sqrt(jumpSpeedSquared);
        isGrounded = false;
    }

    /// <summary>
    /// 입력의 x·y를 몸 기준 오른쪽·앞쪽 방향으로 바꾸고 속도를 적용합니다.
    /// 수평 속도(m/s)를 반환하며, Motor의 필드는 변경하지 않습니다.
    /// </summary>
    private Vector3 CalculateHorizontalVelocity(Vector2 move, float speed)
    {
        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 moveDirection = right * move.x + forward * move.y;
        return moveDirection * speed;
    }

    /// <summary>
    /// 이번 프레임의 수직 이동량(m)을 반환하고 verticalVelocity를 갱신합니다.
    /// 공중에서는 현재 속도로 이동량을 먼저 계산한 뒤 중력으로 속도를 줄입니다.
    /// </summary>
    private float CalculateVerticalDistance(float deltaTime)
    {
        if (isGrounded)
        {
            verticalVelocity = 0f;
            return -c_groundStickSpeed * deltaTime;
        }

        // 변위 = 현재 속도 * 시간 - 0.5 * 중력 * 시간². 중력은 양수 크기이며 아래로 적용합니다.
        float velocityDistance = verticalVelocity * deltaTime;
        float gravityDistance = 0.5f * settings.Gravity * deltaTime * deltaTime;
        float verticalDistance = velocityDistance - gravityDistance;
        verticalVelocity -= settings.Gravity * deltaTime;
        return verticalDistance;
    }

    /// <summary>
    /// 접지 중에는 현재 캡슐 크기에 맞게 stepOffset을 제한하고, 공중에서는 0으로 설정합니다.
    /// 로컬 캡슐 길이를 월드 길이로 바꾼 뒤 원래 계단 설정과 비교합니다.
    /// </summary>
    private void UpdateStepOffset()
    {
        float capsuleDiameter = characterController.radius * 2f;
        float availableStepHeight = (characterController.height - capsuleDiameter) * worldScale;
        float maxStepOffset = Mathf.Max(availableStepHeight, 0f);
        characterController.stepOffset = isGrounded ? Mathf.Min(standingStepOffset, maxStepOffset) : 0f;
    }

    /// <summary>
    /// Move의 천장·바닥 충돌 결과를 반영해 verticalVelocity와 isGrounded를 갱신합니다.
    /// 천장 충돌로 상승을 멈춘 뒤 접지를 판단하고, 착지했다면 수직 속도를 0으로 만듭니다.
    /// </summary>
    private void ApplyCollisionResult(CollisionFlags collisions)
    {
        // CollisionFlags는 여러 충돌 방향을 비트로 담습니다. &로 해당 방향의 비트를 확인합니다.
        bool hitCeiling = (collisions & CollisionFlags.Above) != 0;
        if (hitCeiling && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        // ||의 단락 평가를 유지해 Move가 바닥 충돌을 알려주면 추가 검사는 생략합니다.
        isGrounded = verticalVelocity <= 0f
            && ((collisions & CollisionFlags.Below) != 0 || ProbeGround());
        if (isGrounded)
        {
            verticalVelocity = 0f;
        }
    }
}
