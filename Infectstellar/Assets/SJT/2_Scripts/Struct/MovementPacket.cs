using UnityEngine;

public struct MovementPacket
{
    private PlayerState state;
    private bool isCrouching;
    private Vector2 direction;

    public PlayerState State => state;
    public bool IsCrouching => isCrouching;
    public Vector2 Direction => direction;

    public MovementPacket(PlayerState _state, bool _isCrouching, Vector2 _direction)
    {
        state = _state;
        isCrouching = _isCrouching;
        direction = _direction;
    }

    public byte Pack()
    {
        // WASD의 정지와 8방향을 3×3 칸의 번호 0~8로 바꿉니다.
        int x = Mathf.RoundToInt(direction.x);
        int y = Mathf.RoundToInt(direction.y);
        int directionCode = (y + 1) * 3 + (x + 1);
        int crouching = isCrouching ? 1 : 0;

        // 비트 7~4는 방향, 비트 3은 웅크림, 비트 2~0은 상태입니다.
        return (byte)((directionCode << 4) | (crouching << 3) | (int)state);
    }

    public static MovementPacket Unpack(byte packed)
    {
        var state = (PlayerState)(packed & 0b0111);
        bool isCrouching = (packed & 0b1000) != 0;
        int directionCode = packed >> 4;
        int x = directionCode % 3 - 1;
        int y = directionCode / 3 - 1;

        // 대각선은 기존 WASD 입력과 같은 정규화 벡터로 복원합니다.
        Vector2 direction = new Vector2(x, y).normalized;
        return new MovementPacket(state, isCrouching, direction);
    }
}