namespace Imlight.Server.Data.Statistics;

public static class ClassStats
{
    public const int StartMana = 15;
    public const int StartGold = 0;
    // There's some tomfoolery happening here. Some levels give 3 mana rather than 2.
    public const int ManaPerLevel = 2;
    
    public const int FireStartHealth = 415;
    public const int IceStartHealth = 500;
    public const int StormStartHealth = 400;
    public const int MythStartHealth = 415;
    public const int LifeStartHealth = 460;
    public const int DeathStartHealth = 450;
    public const int BalanceStartHealth = 480;

    // These stats are not actually constant on live servers. There is some algorithm that determines how much health
    // you get per level. For prototype purposes I'm not going to try to figure it out.
    public const int FireHealthPerLevel = 22;
    public const int IceHealthPerLevel = 31;
    public const int StormHealthPerLevel = 17;
    public const int MythHealthPerLevel = 23;
    public const int DeathHealthPerLevel = 24;
    public const int BalanceHealthPerLevel = 27;
}