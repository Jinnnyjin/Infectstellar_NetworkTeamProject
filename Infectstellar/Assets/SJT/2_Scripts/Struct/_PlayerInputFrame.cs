using UnityEngine;

/// <summary>
/// 한 프레임의 이동 의도와 점프 신규 입력을 전달합니다.
/// </summary>
internal readonly struct _PlayerInputFrame
{
    private readonly Vector2 move;
    private readonly bool runHeld;
    private readonly bool crouchHeld;
    private readonly bool jumpPressed;

    /// <summary>
    /// 크기가 1 이하인 앞뒤·좌우 이동 입력입니다.
    /// </summary>
    internal Vector2 Move => move;

    /// <summary>
    /// 달리기 버튼을 누르는 중인지 반환합니다.
    /// </summary>
    internal bool RunHeld => runHeld;

    /// <summary>
    /// 웅크리기 버튼을 누르는 중인지 반환합니다.
    /// </summary>
    internal bool CrouchHeld => crouchHeld;

    /// <summary>
    /// 이번 프레임에 점프 버튼을 새로 눌렀는지 반환합니다.
    /// </summary>
    internal bool JumpPressed => jumpPressed;

    /// <summary>
    /// 입력 크기를 제한하고 이번 프레임의 의도를 보관합니다.
    /// </summary>
    internal _PlayerInputFrame(Vector2 move, bool runHeld, bool crouchHeld, bool jumpPressed)
    {
        this.move = Vector2.ClampMagnitude(move, 1f);
        this.runHeld = runHeld;
        this.crouchHeld = crouchHeld;
        this.jumpPressed = jumpPressed;
    }
}
