/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.Common.Configuration;
using Imlight.CoreLib.WizardData.Implementations;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Behaviors;

public enum MagicSchool {
    None = 0,
    Ice = 72777,
    Life = 2330892,
    Fire = 2343174,
    Myth = 2448141,
    Death = 78318724,
    Storm = 83375795,
    Balance = 1027491821,
    Sun = 78483,
    Star = 2625203,
    Moon = 2504141,
    Shadow = 1429009101,
}

[Serializable]
public class ServerMagicSchoolBehavior : ServerBehaviorInstance {
    public override bool NoTransfer { get; set; } = false;

    public MagicSchool MagicSchool;
    public int ExperiencePoints;
    public int Level;
    public int TrainingPoints;
    public int OverflowXp;
    public int LevelIsLocked;
    public uint EquippedTeleportEffect;

    public bool SetLevel(byte level) {
        if (level > ConfigurationManager.Settings.MaxLevel) {
            return false;
        }

        Level = level;

        return true;
    }

    public override ClientMagicSchoolBehavior GetClientBehaviorInstance() => new() {
        m_schoolOfFocus = (uint) MagicSchool,
        m_experiencePoints = ExperiencePoints,
        m_level = Level,
        m_trainingPoints = TrainingPoints,
        m_overflowXP = OverflowXp,
        m_levelLocked = LevelIsLocked,
        m_equippedTeleportEffect = EquippedTeleportEffect
    };
}
