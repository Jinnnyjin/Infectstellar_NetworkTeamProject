
using UnityEngine;

public struct PlayerStatusInfo
{
    public string Nickname;
    public bool IsAlive;
    public Color Color;

    public PlayerStatusInfo(string nickname, bool isAlive, Color color)
    {
        Nickname = nickname;
        IsAlive = isAlive;
        Color = color;
    }
}
