
public readonly struct PlayerStatusInfo 
{
    public readonly string Nickname;
    public readonly bool IsAlive;

    public PlayerStatusInfo(string nickname, bool isAlive)
    {
        Nickname = nickname;
        IsAlive = isAlive;
    }
}
