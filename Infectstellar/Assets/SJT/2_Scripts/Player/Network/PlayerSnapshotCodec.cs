using System;
using UnityEngine;

internal static class PlayerSnapshotCodec
{
    public const int PayloadSize = 41;

    public static byte[] Encode(PlayerSnapShot snapshot)
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

        var movement = new MovementPacket(snapshot.State, snapshot.IsCrouching, snapshot.MoveDirection);
        payload[40] = movement.Pack();

        return payload;
    }

    public static PlayerSnapShot Decode(byte[] payload)
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
        MovementPacket movementPacket = MovementPacket.Unpack(payload[40]);

        return new PlayerSnapShot(
            time, position, yRotation, viewPitch,
            movementPacket.State, movementPacket.Direction, movementPacket.IsCrouching, velocity);
    }
}
