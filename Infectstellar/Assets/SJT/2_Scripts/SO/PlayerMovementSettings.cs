using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Infectstellar/Player Movement Settings")]
public sealed class PlayerMovementSettings
{
    [SerializeField, Min(0f)] private float walkSpeed = 3f;
    [SerializeField, Min(0f)] private float runSpeed = 5f;
    [SerializeField, Min(0f)] private float crouchSpeed = 1.5f;
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField, Min(0.01f)] private float gravity = 20f;
    [SerializeField, Range(0.1f, 1f)] private float crouchHeightRatio = 0.6f;

    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float CrouchSpeed => crouchSpeed;
    public float JumpHeight => jumpHeight;
    public float Gravity => gravity;
    public float CrouchHeightRatio => crouchHeightRatio; // 서 있는 캡슐 높이에 대한 웅크린 높이의 비율을 반환합니다.

    public bool IsValid()
    {
        return IsNonNegative(walkSpeed)
        && IsNonNegative(runSpeed)
        && IsNonNegative(crouchSpeed)
        && IsNonNegative(jumpHeight)
        && IsNonNegative(gravity)
        && gravity > 0f
        && IsNonNegative(crouchHeightRatio)
        && (crouchHeightRatio > 0f && crouchHeightRatio <= 1f);
    }

    private bool IsNonNegative(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
