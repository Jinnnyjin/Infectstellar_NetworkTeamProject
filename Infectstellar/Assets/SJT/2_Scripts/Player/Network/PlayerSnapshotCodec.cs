using System;
using UnityEngine;

internal static class PlayerSnapshotCodec
{
    public const int PayloadSize = 41;

    public static byte[] Encode(_PlayerSnapShot snapshot)
    {
        var payload = new byte[PayloadSize];
        Span<byte> bytes = payload;

        BitConverter.TryWriteBytes(bytes.Slice(0, 8), snapshot.Time);
        BitConverter.TryWriteBytes(bytes.Slice(8, 4), snapshot.Position.x);
        BitConverter.TryWriteBytes(bytes.Slice(12, 4), snapshot.Position.y);
        BitConverter.TryWriteBytes(bytes.Slice(16, 4), snapshot.Position.z);
        BitConverter.TryWriteBytes(bytes.Slice(20, 4), snapshot.YRotation);
        BitConverter.TryWriteBytes(bytes.Slice(24, 4), snapshot.ViewPitch);
        BitConverter.TryWriteBytes(bytes.Slice(28, 4), snapshot.Velocity.x);
        BitConverter.TryWriteBytes(bytes.Slice(32, 4), snapshot.Velocity.y);
        BitConverter.TryWriteBytes(bytes.Slice(36, 4), snapshot.Velocity.z);

        var movement = new MovementState(
            snapshot.State, snapshot.IsCrouching, snapshot.MoveDirection);
        payload[40] = movement.Pack();

        return payload;
    }

    public static _PlayerSnapShot Decode(byte[] payload)
    {
        double time = BitConverter.ToDouble(payload, 0);
        var position = new Vector3(
            BitConverter.ToSingle(payload, 8),
            BitConverter.ToSingle(payload, 12),
            BitConverter.ToSingle(payload, 16));
        float yRotation = BitConverter.ToSingle(payload, 20);
        float viewPitch = BitConverter.ToSingle(payload, 24);
        var velocity = new Vector3(
            BitConverter.ToSingle(payload, 28),
            BitConverter.ToSingle(payload, 32),
            BitConverter.ToSingle(payload, 36));
        MovementState movement = MovementState.Unpack(payload[40]);

        return new _PlayerSnapShot(
            time, position, yRotation, viewPitch,
            movement.State, movement.Direction, movement.IsCrouching, velocity);
    }

    private readonly struct MovementState
    {
        public readonly PlayerState State;
        public readonly bool IsCrouching;
        public readonly Vector2 Direction;

        public MovementState(PlayerState state, bool isCrouching, Vector2 direction)
        {
            State = state;
            IsCrouching = isCrouching;
            Direction = direction;
        }

        public byte Pack()
        {
            // WASD의 정지와 8방향을 3×3 칸의 번호 0~8로 바꿉니다.
            int x = Mathf.RoundToInt(Direction.x);
            int y = Mathf.RoundToInt(Direction.y);
            int directionCode = (y + 1) * 3 + (x + 1);
            int crouching = IsCrouching ? 1 : 0;

            // 비트 7~4는 방향, 비트 3은 웅크림, 비트 2~0은 상태입니다.
            return (byte)((directionCode << 4) | (crouching << 3) | (int)State);
        }

        public static MovementState Unpack(byte packed)
        {
            var state = (PlayerState)(packed & 0b0111);
            bool isCrouching = (packed & 0b1000) != 0;
            int directionCode = packed >> 4;
            int x = directionCode % 3 - 1;
            int y = directionCode / 3 - 1;

            // 대각선은 기존 WASD 입력과 같은 정규화 벡터로 복원합니다.
            Vector2 direction = new Vector2(x, y).normalized;
            return new MovementState(state, isCrouching, direction);
        }
    }
}
