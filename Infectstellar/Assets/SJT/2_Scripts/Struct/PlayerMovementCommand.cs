using UnityEngine;

/// <summary>
/// HFSM이 결정한 한 프레임의 이동 명령입니다. 물리 실행은 Motor가 담당합니다.
/// </summary>
public readonly struct PlayerMovementCommand
{
    private readonly Vector2 move;
    private readonly float speed;
    private readonly bool jump;
    private readonly bool crouch;

    public Vector2 Move => move;
    public float Speed => speed;
    public bool Jump => jump;
    public bool Crouch => crouch;

    public PlayerMovementCommand(Vector2 _move, float _speed, bool _jump, bool _crouch)
    {
        move = _move;
        speed = _speed;
        jump = _jump;
        crouch = _crouch;
    }
}
