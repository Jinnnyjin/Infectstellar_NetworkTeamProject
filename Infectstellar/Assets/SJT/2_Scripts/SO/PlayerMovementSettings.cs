using UnityEngine;

/// <summary>
/// 플레이어의 이동 속도, 점프와 웅크리기 설정을 보관합니다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Infectstellar/Player Movement Settings")]
public sealed class PlayerMovementSettings : ScriptableObject
{
    [SerializeField, Min(0f)] private float walkSpeed = 3f;
    [SerializeField, Min(0f)] private float runSpeed = 5f;
    [SerializeField, Min(0f)] private float crouchSpeed = 1.5f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField, Min(0.01f)] private float gravity = 20f;
    [SerializeField, Range(0.1f, 1f)] private float crouchHeightRatio = 0.6f;

    /// <summary>
    /// 걷기 속도를 m/s로 반환합니다.
    /// </summary>
    public float WalkSpeed => walkSpeed;

    /// <summary>
    /// 달리기 속도를 m/s로 반환합니다.
    /// </summary>
    public float RunSpeed => runSpeed;

    /// <summary>
    /// 웅크리기 이동 속도를 m/s로 반환합니다.
    /// </summary>
    public float CrouchSpeed => crouchSpeed;

    /// <summary>
    /// 점프 높이를 m로 반환합니다.
    /// </summary>
    public float JumpHeight => jumpHeight;

    /// <summary>
    /// 아래로 작용하는 중력의 크기를 m/s²로 반환합니다.
    /// </summary>
    public float Gravity => gravity;

    /// <summary>
    /// 서 있는 캡슐 높이에 대한 웅크린 높이의 비율을 반환합니다.
    /// </summary>
    public float CrouchHeightRatio => crouchHeightRatio;

    /// <summary>
    /// 필수 이동 설정의 유한 값과 허용 범위를 확인합니다.
    /// </summary>
    internal bool IsValid()
    {
        return IsNonNegative(walkSpeed) && IsNonNegative(runSpeed) && IsNonNegative(crouchSpeed)
            && IsNonNegative(jumpHeight) && IsNonNegative(gravity) && gravity > 0f
            && IsNonNegative(crouchHeightRatio) && crouchHeightRatio > 0f && crouchHeightRatio <= 1f;
    }

    /// <summary>
    /// 음수가 아닌 유한 값인지 확인합니다.
    /// </summary>
    private static bool IsNonNegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
