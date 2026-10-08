using UnityEngine;

/// <summary>
/// HFSM이 결정한 한 프레임의 이동 명령입니다. 물리 실행은 Motor가 담당합니다.
/// </summary>
internal readonly struct _PlayerMovementCommand
{
    private readonly Vector2 move;
    private readonly float speed;
    private readonly bool jump;
    private readonly bool crouch;

    /// <summary>
    /// 허용된 수평 이동 입력입니다.
    /// </summary>
    internal Vector2 Move => move;

    /// <summary>
    /// 선택한 이동 속도를 m/s로 반환합니다.
    /// </summary>
    internal float Speed => speed;

    /// <summary>
    /// 이번 물리 실행에서 점프를 한 번 시작할지 반환합니다.
    /// </summary>
    internal bool Jump => jump;

    /// <summary>
    /// 유지할 충돌 캡슐의 웅크림 여부입니다.
    /// </summary>
    internal bool Crouch => crouch;

    /// <summary>
    /// 상태가 선택한 이동과 자세 명령을 보관합니다.
    /// </summary>
    internal _PlayerMovementCommand(Vector2 move, float speed, bool jump, bool crouch)
    {
        this.move = move;
        this.speed = speed;
        this.jump = jump;
        this.crouch = crouch;
    }
}
