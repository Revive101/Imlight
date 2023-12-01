/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.WizardData.Implementations;

/// <summary>
/// Contains base stats for each class, as well as stats that are calculated based on level.
/// </summary>
public static class WizardClassData {
    private const int StartMana = 15;
    private const int StartGold = 0;
    // There's some tomfoolery happening here. Some levels give 3 mana rather than 2.
    private const int ManaPerLevel = 2;

    private const int FireStartHealth = 415;
    private const int IceStartHealth = 500;
    private const int StormStartHealth = 400;
    private const int MythStartHealth = 415;
    private const int LifeStartHealth = 460;
    private const int DeathStartHealth = 450;
    private const int BalanceStartHealth = 480;

    // These stats are not actually constant on live servers. There is some algorithm that determines how much health
    // you get per level. For prototype purposes I'm not going to try to figure it out.
    private const int FireHealthPerLevel = 22;
    private const int IceHealthPerLevel = 31;
    private const int StormHealthPerLevel = 17;
    private const int MythHealthPerLevel = 23;
    private const int DeathHealthPerLevel = 24;
    private const int BalanceHealthPerLevel = 27;

    public static int GetClassStartingHealth(WizardSchool school) {
        return school switch {
            WizardSchool.Fire => FireStartHealth,
            WizardSchool.Ice => IceStartHealth,
            WizardSchool.Storm => StormStartHealth,
            WizardSchool.Myth => MythStartHealth,
            WizardSchool.Life => LifeStartHealth,
            WizardSchool.Death => DeathStartHealth,
            WizardSchool.Balance => BalanceStartHealth,
            _ => throw new ArgumentOutOfRangeException(nameof(school), school, null)
        };
    }

    public static int GetClassHealthAtLevel(WizardSchool school, int level) {
        return school switch {
            WizardSchool.Fire => FireStartHealth + (FireHealthPerLevel * (level - 1)),
            WizardSchool.Ice => IceStartHealth + (IceHealthPerLevel * (level - 1)),
            WizardSchool.Storm => StormStartHealth + (StormHealthPerLevel * (level - 1)),
            WizardSchool.Myth => MythStartHealth + (MythHealthPerLevel * (level - 1)),
            WizardSchool.Life => LifeStartHealth + (MythHealthPerLevel * (level - 1)),
            WizardSchool.Death => DeathStartHealth + (DeathHealthPerLevel * (level - 1)),
            WizardSchool.Balance => BalanceStartHealth + (BalanceHealthPerLevel * (level - 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(school), school, null)
        };
    }

    public static int GetManaAtLevel(int level) {
        return StartMana + (ManaPerLevel * (level - 1));
    }

    public static float GetPowerPipChanceAtLevel(int level) {
        // Wizards do not have a power pip chance until level 10.
        return level < 10 ? 0f :
            // Every subsequent level they gain 1%, to a max of 40%.
            Math.Min(0.4f, (level - 10) * 0.01f);
    }

    public static int GetPetEnergyAtLevel(int level) {
        // Wizards start with 40 energy, and gain 1 energy every 2 levels, until they reach a max of 130 energy.
        // At level 7, they get an increase of 10 energy.
        return Math.Min(130, 40 + ((level - 1) / 2) + (level >= 7 ? 10 : 0));
    }
}
