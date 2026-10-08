using UnityEngine;

/// <summary>
/// 한 프레임의 이동 의도와 점프 신규 입력을 전달합니다.
/// </summary>
public readonly struct PlayerInputFrame
{
    private readonly Vector2 move;
    private readonly bool runHeld;
    private readonly bool crouchHeld;
    private readonly bool jumpPressed;

    public Vector2 Move => move; // 앞뒤,좌우 이동 입력
    public bool RunHeld => runHeld; // 달리기 버튼을 누르는 중인지
    public bool CrouchHeld => crouchHeld; // 웅크리기 버튼을 누르는 중인지
    public bool JumpPressed => jumpPressed; // 이번 프레임에서 점프 버튼을 새로 눌렀는지 반환

    public PlayerInputFrame(Vector2 _move, bool _runHeld, bool _crouchHeld, bool _jumpPressed)
    {
        move = Vector2.ClampMagnitude(_move, 1f); // Vector2.ClampMagnitude : 벡터의 방향은 유지하면서 크기만 제한
        runHeld = _runHeld;
        crouchHeld = _crouchHeld;
        jumpPressed = _jumpPressed;
    }
}
