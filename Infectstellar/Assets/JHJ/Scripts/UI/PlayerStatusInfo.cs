
using UnityEngine;

public readonly struct PlayerStatusInfo 
{
    public readonly string Nickname;
    public readonly bool IsAlive;
    public readonly Color Color;

    public PlayerStatusInfo(string nickname, bool isAlive, Color color)
    {
        Nickname = nickname;
        IsAlive = isAlive;
        Color = color;
    }
}
