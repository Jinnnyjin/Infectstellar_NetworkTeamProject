using System;
using UnityEngine;

/// <summary>
/// HFSM의 명령으로 CharacterController 이동, 중력과 충돌 자세를 처리.
/// DisallowMultipleComponent : 같은 게임오브젝트에 이 컴포넌트를 두 개 이상 붙이지 못하게 하는 속성
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMotor : MonoBehaviour
{
    #region 상수 필드
    // 접지 중 바닥과의 접촉을 유지하기 위해(stick:붙어있다) 아래로 이동을 시도하는 속도입니다. 단위: m/s.
    private const float c_groundStickSpeed = 2f;
    // 바닥 검사 여유 거리의 최솟값이며, 검사 길이에도 추가합니다. 단위: m.
    private const float c_minProbeDistance = 0.01f;
    // 캡슐 반지름의 10%를 기준으로 검사 시작점과 검사 구의 여유를 정합니다.
    private const float c_queryInsetRatio = 0.1f;
    // NonAlloc 검사에서 재사용할 각 결과 배열의 크기입니다.
    private const int c_queryCapacity = 32;
    #endregion

    #region 물리 검사 결과 배열
    // CanStand()에서 일어설 머리 공간과 겹치는 Collider들을 저장합니다.
    // 일어서기를 막는 장애물이 있는지 확인할 때 사용하며, 같은 배열을 반복해서 재사용합니다.
    private readonly Collider[] overlaps = new Collider[c_queryCapacity];
    // ProbeGround()에서 발밑 구 검사로 발견한 충돌 정보를 저장합니다.
    // 충돌한 대상과 표면 방향을 확인해 설 수 있는 바닥인지 판단하며, 같은 배열을 반복해서 재사용합니다.
    private readonly RaycastHit[] groundHits = new RaycastHit[c_queryCapacity];
    #endregion

    #region 초기화할 때 저장하고 이후 이동과 검사에 재사용하는 참조
    private CharacterController characterController;
    private Transform body;
    private PlayerMovementSettings settings;
    #endregion

    #region Stand 필드
    // 서 있는 충돌 캡슐의 전체 높이
    private float standingHeight;
    // 서 있는 캡슐의 중심. 플레이어 Transform 기준의 로컬 위치야
    private Vector3 standingCenter;
    // 서 있을 때의 계단·턱을 자동으로 올라가는 높이 설정값.
    private float standingStepOffset;
    // 바닥과 일어설 공간을 검사할 때 포함할 레이어 목록, 땅/천장/물건(Props) 레이어를 만들어서 오브젝트에 지정해주면됨
    [SerializeField] private LayerMask collisionMask;
    #endregion

    // 로컬 캡슐 크기를 월드 길이로 바꾸는 균일 스케일
    private float worldScale;

    #region 다음 프레임에도 유지하고, 상태 판단에 전달할 실제 몸의 정보 필드/프로퍼티
    // 수직 속도
    private float verticalVelocity;
    // 현재 수직 속도. 양수면 위쪽
    public float VerticalVelocity => verticalVelocity;
    // 땅에 접지되어 있는지 여부
    private bool isGrounded;
    // 현재 접지 여부. 이륙해 있는 동안은 false
    public bool IsGrounded => isGrounded;
    // 플레이어가 웅크리고 있는지 여부
    private bool isCrouching;
    // 현재 웅크리고 있는지 여부. 웅크리고 있는 동안은 true
    public bool IsCrouching => isCrouching;
    #endregion

    // Motor와 CharacterController가 활성 상태이며 초기화된 설정이 있는지 확인
    // isActiveAndEnabled :현재 컴포넌트(PlayerMotor)가 활성화되어 있는지 여부를 알려주는 Unity의 읽기 전용 프로퍼티
    public bool CanSimulate => isActiveAndEnabled
        && characterController != null
        && characterController.enabled
        && settings != null;

    /// <summary>
    /// 컴포넌트와 설정을 확인하고, 원래 캡슐 정보와 충돌 검사 대상을 저장합니다.
    /// 준비가 끝나면 현재 접지를 갱신하며, 같은 설정으로 다시 호출하면 재초기화하지 않습니다.
    /// </summary>
    public bool Initialize(PlayerMovementSettings _settings)
    {
        #region 플레이어의 필수 설정 데이터와 컴포넌트가 있는지 확인
        // _settings : 플레이어 이동에 쓰이는 데이터들의 SO
        if (settings != null)
        {
            return settings == _settings;
        }

        characterController = GetComponent<CharacterController>();
        body = transform;

        if (characterController == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerMotor] CharacterController가 필요합니다.", this);
#endif
            return false;
        }

        if (_settings == null || !_settings.IsValid())
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerMotor] 유효한 이동 설정이 필요합니다.", this);
#endif
            return false;
        }
        #endregion

        #region 서 있는 상태 및 전역의 초기 데이터를 설정
        standingHeight = characterController.height;
        standingCenter = characterController.center;
        standingStepOffset = characterController.stepOffset;
        // body.lossyScale : 부모까지 반영한 월드 기준 배율
        // x·y·z 배율이 같은 균일 스케일을 전제로, y 배율을 대표값으로 저장합니다.
        // 이후 CharacterController의 캡슐 크기를 월드 크기로 변환할 때, 매번 lossyScale을 조회하지 않고 저장한 배율을 사용합니다.
        worldScale = body.lossyScale.y;
        #endregion

        settings = _settings;
        RefreshGrounded();
        return true;
    }

    /// <summary>
    /// 충돌 검사에 필요한 캡슐의 아래쪽·위쪽 구의 월드 중심 위치와 월드 반지름을 out으로 전달합니다.
    /// bottom : 캡슐 아래쪽 구의 중심, top : 캡슐 위 쪽 구의 중심, radius : 양 쪽 구의 반지름
    /// Unity의 OverlapCapsuleNonAlloc은 두 구의 중심과 반지름으로 가상의 캡슐 공간을 정해 겹치는 충돌체를 찾습니다.
    /// 여기서 계산한 값은 ProbeGround()의 발밑 검사와 CanStand()의 일어설 공간 검사에 사용합니다.
    /// 몸이 수직으로 서 있고 worldScale이 양수의 균일 스케일이라는 전제로 계산합니다.
    /// </summary>
    /// <param name="height">입력: 위아래 둥근 부분까지 포함한 캡슐 전체 높이입니다. 스케일 적용 전의 로컬 길이입니다.</param>
    /// <param name="center">입력: 플레이어 위치를 기준으로 캡슐 중심이 어디에 있는지 나타냅니다. 예: (0, 1, 0)이면 플레이어 기준 위쪽으로 1만큼 떨어진 위치입니다.</param>
    /// <param name="bottom">출력: 아래쪽 구의 월드 중심 위치입니다. 캡슐 아래쪽 끝 표면보다 반지름만큼 위에 있습니다.</param>
    /// <param name="top">출력: 위쪽 구의 월드 중심 위치입니다. 캡슐 위쪽 끝 표면보다 반지름만큼 아래에 있습니다.</param>
    /// <param name="radius">출력: CharacterController의 반지름에 전역 크기 배율을 적용한 월드 반지름입니다. 위아래 구가 같은 값을 사용합니다.</param>
    private void GetCapsule(float height, Vector3 center, out Vector3 bottomSphereCenter, out Vector3 topSphereCenter, out float radius)
    {
        // TransformPoint는 로컬 위치인 center를 게임 세계에서의 실제 위치로 바꿉니다.
        // 즉, worldCenter는 캡슐 전체 중심이 게임 세계의 어디에 있는지 나타내는 위치입니다.
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
        bottomSphereCenter = worldCenter - Vector3.up * sphereCenterOffset;
        topSphereCenter = worldCenter + Vector3.up * sphereCenterOffset;
    }

    /// <summary>
    /// 물리 검사에 잡힌 충돌체를 “장애물로 취급해도 되는가?” 확인하는 함수
    /// 검사된 충돌체가 실제로 몸을 막는 대상인지 확인합니다.
    /// </summary>
    /// <param name="other"바닥 검사나 일어설 공간 검사에서 발견한 충돌체</param>
    private bool IsBlocking(Collider other)
    {
        // 충돌체가 존재하는가? && 내 몸이나 내 자식의 충돌체가 아닌가?
        return other != null && !other.transform.IsChildOf(body);
    }

    /// <summary>
    /// 발밑으로 검사용 구를 조금 내려 보내서, 서 있을 수 있는 바닥이 있는지 확인한다.
    /// </summary>
    /// <returns></returns>
    private bool ProbeGround()
    {
        // 발 쪽 구의 중심과 반지름을 가져온다
        GetCapsule(characterController.height, characterController.center, out Vector3 bottomSphereCenter, out _, out float radius);

        // 검사 여유 거리 설정. 캡슐의 아래쪽 구의 반지름 * c_queryInsetRatio. 구의 반지름이 너무 작을 경우를 대비하여 최소 거리 c_minProbeDistance를 설정
        float probeDistance = Mathf.Max(radius * c_queryInsetRatio, c_minProbeDistance);

        // 검사 구의 크기 및 위치 설정. 기존 캡슐의 아래쪽 구의 반지름에서 여유거리를 빼고, 여유거리만큼 위쪽으로 올린 검사 구를 만든다.
        float queryRadius = Mathf.Max(radius - probeDistance, radius * 0.5f); // 원래 구에서 여유 거리만큼 반지름을 줄이되, 검사용 구가 너무 작아지지 않도록(probeDistance가 c_minProbeDistance로 설정된 경우) 원래 반지름의 절반은 유지합니다.
        Vector3 castOrigin = bottomSphereCenter + Vector3.up * probeDistance;

        // 검사 구를 내려 보낼 거리. 위로 띄우고 작게 만든 구가 원래 발밑에 도달한 뒤, CharacterController의 접촉 여유까지 바닥을 찾습니다.
        float groundTolerance = Mathf.Max(characterController.skinWidth, c_minProbeDistance);
        float castDistance = probeDistance * 2f + groundTolerance;

        // SphereCast는 광선 대신 일정한 반지름을 가진 구체를 이동시키면서 충돌을 검사, 구체가 이동하는 경로에 장애물이 닿으면 충돌이 감지됨
        // SphereCastNonAlloc은 동일한 구체 이동 검사를 수행하지만, 결과를 사용자가 미리 생성해 놓은 배열에 저장함. 불필요한 메모리 할당을 줄일 수 있음
        // 주의 1 : 시작 위치에서 이미 겹쳐 있는 Collider는 감지하지 못함.
        // 주의 2 : 사용자가 생성한 배열의 크기가 부족하면 일부 충돌 결과가 누락됨.
        int count = Physics.SphereCastNonAlloc(
            castOrigin, // 검사 구의 시작 위치
            queryRadius, // 검사 구의 크기
            Vector3.down, // 검사 구가 움직일 방향
            groundHits, // 발견한 Collider들을 저장할 배열
            castDistance, // 검사 구가 움직일 거리
            collisionMask, // 검사에 포함할 레이어들
            QueryTriggerInteraction.Ignore); // QueryTriggerInteraction.Ignore : Trigger Collider는 검사 대상에서 제외

        // 발견한 물체가 서 있을 수 있는 바닥인지 확인한다
        // characterController.slopeLimit : 허용할 최대 경사 각도
        // Deg2Rad : 각도를 라디안 단위로 바꾸는 숫자, Mathf.Cos()는 라디안으로 입력받은 각도를 받기 때문
        // Mathf.Cos : 특정 각도의 코사인(Cosine) 값을 계산해서 반환하는 함수
        // 코사인(Cosine) 값은 특정 각도가 주어졌을 때, 그 각도에 따라 결정되는 -1부터 1 사이의 숫자
        // minGroundDot은 Mathf.Cos로 받은 코사인 값과 groundDot 코사인 값과 비교에 사용할 바닥으로 인정할 수 있는 최소 내적 기준값. 너무 가파른 면을 바닥에서 제외하기 위한 기준
        float minGroundDot = Mathf.Cos(characterController.slopeLimit * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (!IsBlocking(hit.collider)) // 충돌한 대상 중 제외 대상은 거름
            {
                continue;
            }

            // Mathf.Cos: 라디안 단위의 각도로 코사인 값을 구하는 함수.
            // Vector3.Dot: 두 벡터의 내적값을 구하는 함수. 두 벡터의 길이가 모두 1이면, 두 방향 사이 각도의 코사인 값과 같음.
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
    public void RefreshGrounded()
    {
        isGrounded = verticalVelocity <= 0f && ProbeGround();
    }

    /// <summary>
    /// 웅크린 몸이 일어설 때 머리 위 공간을 막는 충돌체가 있는지 검사
    /// </summary>
    /// <returns>현재 일어날 수 있는지 여부</returns>
    public bool CanStand()
    {
        if (!isCrouching) // 웅크린 상태가 아니라면 바로 true를 반환
        {
            return true;
        }

        // 서 있을 때 머리 위치 계산
        GetCapsule(standingHeight, standingCenter, out _, out Vector3 standingTopSphereCenter, out float radius);
        // 현재 웅크린 머리 위치 계산
        GetCapsule(characterController.height, characterController.center, out _, out Vector3 crouchingTopSphereCenter, out _);

        // 현재 머리부터 선 자세의 머리까지 원래 반지름인 캡슐 영역 안에 콜라이더들을 검사
        // Physics.OverlapCapsule()은 캡슐 모양의 영역과 겹치는 Collider들을 검사하는 함수.
        // OverlapCapsuleNonAlloc은 동일한 캡슐 검사를 수행하지만, 결과를 사용자가 미리 생성해 놓은 배열에 저장함. 불필요한 메모리 할당을 줄일 수 있음

        int count = Physics.OverlapCapsuleNonAlloc(
    crouchingTopSphereCenter, // 캡슐 아래 쪽(현재 웅크린 머리 쪽) 구의 월드 중심
    standingTopSphereCenter,  // 캡슐 위 쪽(일어섰을 때 머리 쪽) 구의 월드 중심
    radius,                  // 검사 캡슐의 월드 반지름
    overlaps,                // 발견한 Collider들을 저장할 배열
    collisionMask,           // 검사에 포함할 레이어들
    QueryTriggerInteraction.Ignore); // Trigger Collider는 제외

        // 배열이 꽉 차면 저장하지 못한 장애물이 있을 수 있어 일어서기를 거부
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
    /// 플레이어의 웅크림/서있음 상태를 설정
    /// 발 위치를 유지하며 캡슐 높이,중심과 isCrouching을 변경합니다.
    /// </summary>
    /// <param name="crouchRequested">웅크림 요청</param>
    public void SetCrouching(bool crouchRequested)
    {
        if (isCrouching == crouchRequested) // 이미 웅크린 경우 종료
        {
            return;
        }

        if (!crouchRequested && !CanStand()) // 웅크림 요청이 풀렸을 때, 서있을 수 없는 상태면 종료
        {
            return;
        }

        // 플레이어의 키 설정 : 웅크림 요청이 있으면 서 있는 키에서 웅크렸을 때 비율로 설정
        float height = crouchRequested ? standingHeight * settings.CrouchHeightRatio : standingHeight;
        characterController.height = height;

        // 줄어든 높이의 절반만큼 중심도 내려야 캡슐의 발 위치가 유지됨.
        float centerOffset = (standingHeight - height) * 0.5f;
        characterController.center = standingCenter - Vector3.up * centerOffset;

        // 웅크리고 있는지 여부를 crouchRequested로 설정
        isCrouching = crouchRequested;
    }

    /// <summary>
    ///  한 프레임의 명령을 '웅크림 → 점프 → 이동 계산 → 실제 이동 및 충돌 반영' 순서로 실행합니다
    /// 중력 갱신과 CharacterController.Move는 이 흐름에서 각각 한 번만 수행합니다.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="deltaTime"></param>
    public void Simulate(PlayerMovementCommand command, float deltaTime)
    {
        #region 웅크림
        SetCrouching(command.Crouch);
        #endregion

        #region 점프
        TryStartJump(command.Jump);
        #endregion

        #region 이동 계산
        Vector3 horizontalVelocity = CalculateHorizontalVelocity(command.Move, command.Speed); // 현재 프레임의 수평 속도
        float verticalDistance = CalculateVerticalDistance(deltaTime); // 현재 프레임의 수직 이동 거리
        UpdateStepOffset(); // 현재 오를 수 있는 계단 턱을 characterController에 설정
        Vector3 movement = horizontalVelocity * deltaTime + Vector3.up * verticalDistance; // 실제 이동 벡터 계산
        #endregion

        #region  실제 이동 및 충돌 반영
        // CollisionFlags는 CharacterController가 이동하면서 “어느 방향에 부딪혔는지”를 알려주는 값
        CollisionFlags collisions = characterController.Move(movement);
        // 이동 중 천장이나 바닥에 부딪힌 결과를 반영해 수직 속도와 접지 상태를 갱신
        UpdateVerticalState(collisions);
        #endregion
    }

    /// <summary>
    /// 점프 명령이 있고 접지 중이며 실제로 서 있을 때만 점프를 시작합니다.
    /// 점프를 시작하면 verticalVelocity를 설정하고 isGrounded를 즉시 false로 변경합니다.
    /// </summary>
    /// <param name="jumpRequested">점프 요청 변수 (true : 점프 요청)</param>
    private void TryStartJump(bool jumpRequested)
    {
        // 점프 입력이 없었거나, 이미 공중에 있는 상태거나, 웅크려 있는 상태면 점프를 시도하지 않음.
        if (!jumpRequested || !isGrounded || isCrouching)
        {
            return;
        }

        // 원하는 높이 h에 도달할 초기 속도는 sqrt(2 * g* h)임.
        /// settings.Gravity : 위쪽 속도를 초당 얼마나 줄일지 나타내는 중력의 크기
        /// settings.JumpHeight : 출발 위치에서 올라가고 싶은 높이
        /// 2f : 물리 공식에 들어가는 고정 계수
        /// jumpSpeedSquared : 필요한 출발 속도의 제곱값
        /// 목표 높이까지 올라갈 출발 속도의 제곱값을 구합니다: 속도² = 2 × 중력 × 높이.
        /// 2는 일정한 중력으로 속도가 줄어드는 운동 공식의 고정 계수입니다.
        float jumpSpeedSquared = 2f * settings.Gravity * settings.JumpHeight;
        /// Mathf.Sqrt : 제곱근을 구해서 실제 출발 속도로 바꿈
        /// verticalVelocity : 계산한 속도를 저장. 양수 위쪽으로 올라가게 됨
        verticalVelocity = Mathf.Sqrt(jumpSpeedSquared);

        // 공중에 있는 상태로 변환
        isGrounded = false;
    }

    /// <summary>
    /// 입력의 x·y를 몸 기준 오른쪽·앞쪽 방향으로 바꾸고 속도를 적용합니다. 수평 속도(m/s)를 반환
    /// </summary>
    /// <param name="move">이동 입력. x는 좌우, y는 앞뒤를 나타냅니다.</param>
    /// <param name="speed">이동에 적용할 속력입니다. 단위: m/s.</param>
    /// <returns>입력 방향과 속력을 반영한 월드 기준 수평 속도 벡터입니다. 단위: m/s.</returns>
    private Vector3 CalculateHorizontalVelocity(Vector2 move, float speed)
    {
        // ProjectOnPlane은 방향 벡터를 지정한 평면에 눕힙니다.
        // Vector3.up을 평면의 법선으로 넣어 앞 방향의 위아래 성분을 없애고, 길이를 1로 맞춥니다.
        Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;

        // Cross(외적)는 두 방향 모두에 직각인 벡터를 구합니다.
        // 위쪽, 앞쪽 순서로 넣으면 플레이어의 오른쪽 방향이 나옵니다.
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 moveDirection = right * move.x + forward * move.y;
        return moveDirection * speed;
    }

    /// <summary>
    /// 이번 프레임에 위아래로 얼마나 이동할지를 계산하고, 다음 프레임에 사용할 수직 속도를 갱신하는 메서드
    /// 이번 프레임의 수직 이동량(m)을 반환하고 verticalVelocity를 갱신
    /// 공중에서는 현재 속도로 이동량을 먼저 계산한 뒤 중력으로 속도를 줄임
    /// </summary>
    /// <param name="deltaTime">이번 계산에서 사용할 시간이고, 단위는 초</param>
    /// <returns>수직 이동 거리</returns>
    private float CalculateVerticalDistance(float deltaTime)
    {
        #region 바닥에 있는 경우
        if (isGrounded)
        {
            // 바닥에 닿아있으므로 이전의 공중에서 사용하던 상승·낙하 속도가 있었을 경우 지웁니다. 
            verticalVelocity = 0f;
            // 바닥과 계속 접촉하도록 아래로 조금 이동을 시도합니다.
            return -c_groundStickSpeed * deltaTime;
        }
        #endregion

        #region  공중에 뜬 경우
        // 중력이 없고 현재 속도가 그대로 유지된다면 얼마나 이동할지 구합니다.
        float velocityDistance = verticalVelocity * deltaTime;

        // 멈춰 있던 공을 놓으면, 속력 0에서 시작해 아래로 점점 빨라집니다.
        // Gravity × deltaTime은 그 시간이 끝났을 때 공이 떨어지는 속력입니다.
        // 일정하게 빨라지므로, 그동안의 평균 속력은 끝 속력의 절반입니다. 그래서 0.5를 곱합니다.
        // 평균 속력에 시간을 다시 곱하면, 중력 때문에 떨어지는 거리가 됩니다.
        // 점프 중에는 이 값을 위쪽 이동량에서 빼서, 중력이 올라가는 양을 줄이도록 합니다.
        float gravityDistance = settings.Gravity * deltaTime * 0.5f * deltaTime;

        // 현재 속도로 이동할 양에서 중력의 영향을 뺍니다. 결과가 양수면 위로, 음수면 아래로 이동합니다.
        float verticalDistance = velocityDistance - gravityDistance;
        verticalVelocity -= settings.Gravity * deltaTime;
        return verticalDistance;
        #endregion
    }

    /// <summary>
    /// UpdateStepOffset() : 현재 자세와 접지 상태에 맞춰, 자동으로 올라갈 수 있는 턱의 높이를 조절하는 메서드
    /// </summary>
    private void UpdateStepOffset()
    {
        #region 현재 characterController 캡슐의 가운데 원통 길이 구하기 (캡슐 - 양쪽 구 반지름)
        // 캡슐의 지름 : 캡슐 반지름 * 2
        float capsuleDiameter = characterController.radius * 2f;
        // 전체 높이에서 위아래 둥근 끝의 높이 합(지름)을 빼면 가운데 원통 높이가 남음. worldScale을 곱해 실제 월드 원통 높이로 바꿈
        // 이 코드는 그 원통 높이를 계단 설정의 상한으로 사용
        float availableStepHeight = (characterController.height - capsuleDiameter) * worldScale;
        #endregion

        // 계단 높이 설정에 음수가 들어가지 않도록, 계산 결과가 음수면 0으로 바꿉니다.
        float maxStepOffset = Mathf.Max(availableStepHeight, 0f);

        // 바닥에 있다면 기본 계단 설정(초기 서 있는 상태)과 현재 몸 크기(웅크림 상태면 작아짐)로 정한 상한 중 작은 값을 사용
        // 웅크려서 maxStepOffset이 기본 계단 높이보다 작아지면, 올라갈 수 있는 턱 높이도 그 값으로 낮춤.
        // 보편적으로 서 있는 상태에서는 standingStepOffset가, 웅크린 상태에서는 maxStepOffset가 CharacterController.stepOffset로 설정
        // 공중이라면 0으로 설정해 계단을 자동으로 올라가는 보정을 끕니다.
        // CharacterController.stepOffset : 낮은 계단이나 턱을 만났을 때, 점프 없이 올라가도록 도와주는 기능에서 그 높이 기준값
        characterController.stepOffset = isGrounded ? Mathf.Min(standingStepOffset, maxStepOffset) : 0f;
    }

    /// <summary>
    /// Move의 충돌 결과를 바탕으로 수직 속도와 접지 상태를 갱신합니다.
    /// 상승 중 천장에 부딪히면 상승 속도를 지우고, 위로 올라가는 중이 아니면 바닥 충돌 또는 발밑 검사로 접지를 확인합니다.
    /// 플레이어가 땅에 있는지를 설정하고(isGrounded), 땅에 있다고 판단하면 낙하 속도를 제거함
    /// </summary>
    /// <param name="collisions">CharacterController가 이동하는 과정에서 어느 방향으로 충돌했는지를 나타내는 열거형(Enum)</param>
    private void UpdateVerticalState(CollisionFlags collisions)
    {
        #region CollisionFlags 설명 주석
        // CollisionFlags는 CharacterController.Move() 실행 시 캐릭터가 어느 방향에서 충돌했는지 나타내는 열거형.
        // None: 충돌 없음
        // Sides: 측면 충돌
        // Above: 위쪽 충돌
        // Below: 아래쪽 충돌
        // 단, 이 값들은 반드시 하나만 반환되는 것은 아님. 바닥과 벽에 동시에 충돌했다면 Below와 Sides가 함께 포함될 수 있음.
        // 여러 충돌 상태가 동시에 포함될 수 있으므로 & 연산자로 검사할 수 있음.
        #endregion
        #region 천장에 부딪혔는지 확인하고, 부딪혔다면 상승하는 수직 속도를 초기화
        bool hitCeiling = (collisions & CollisionFlags.Above) != 0;
        if (hitCeiling && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
        #endregion

        #region 발 밑 검사 후 땅에 있다고 판단하면 isGrounded를 true로 설정하고, 낙하 속도를 제거함
        isGrounded = verticalVelocity <= 0f && ((collisions & CollisionFlags.Below) != 0 || ProbeGround());
        // 바닥인게 확인되었다면 낙하 속도 제거
        if (isGrounded)
        {
            verticalVelocity = 0f;
        }
        #endregion
    }
}
