using Imlight.Common.Configuration;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.Common.Utilities;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class CharacterHelper {
    public const float OrientationCompressionFactor = 0.708f;

    public static Wizard CreateCharacterFromCreationInfo(WizardCharacterCreationInfo creationInfo) {
        var character = new Wizard((WizardSchoolEnum) creationInfo.m_schoolOfFocus) {
            WizardAvatar = creationInfo.m_avatarBehavior,
            NameIndices = creationInfo.m_nameIndices,
        };

        // Create the game stats and calculate the base stats.
        var gameStats = new WizGameStats();
        gameStats = SetCharacterStatsToBase(gameStats, character.School.Level, character.School.Type);
        character.GameStats = gameStats;

        return character;
    }

    public static WizardCharacterCreationInfo GetLoginScreenInfo(Wizard character) {
        var creationInfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = character.WizardAvatar,
            m_nameIndices = character.NameIndices,
            m_schoolOfFocus = (uint) character.School.Type,
            m_level = character.School.Level,
            m_name = character.NameOverride,
            m_location = character.ZoneDisplayName,
            m_globalID = (GID) character.CharId,
            m_templateID = 1,
            m_userID = (GID) character.AccountId,
            // TODO: Equipment list
        };
        return creationInfo;
    }

    private static WizGameStats SetCharacterStatsToBase(WizGameStats existingStats, byte level, WizardSchoolEnum school) {
        var baseHealth = WizardClassData.GetClassHealthAtLevel(school, level);
        var baseMana = WizardClassData.GetManaAtLevel(level);

        existingStats.m_baseHitpoints = baseHealth;
        existingStats.m_currentHitpoints = baseHealth;
        existingStats.m_baseMana = baseMana;
        existingStats.m_currentMana = baseMana;
        existingStats.m_baseGoldPouch = ConfigurationManager.Settings.BaseGoldPouch;
        existingStats.m_powerPipBase = WizardClassData.GetPowerPipChanceAtLevel(level);
        existingStats.m_energyMax = WizardClassData.GetPetEnergyAtLevel(level);

        // Initialize the lists.
        existingStats.m_blockPercentBySchool = new List<float>();
        existingStats.m_blockRatingBySchool = new List<float>();
        existingStats.m_dmgBonusFlat = new List<float>();
        existingStats.m_dmgBonusPercent = new List<float>();
        existingStats.m_dmgBonusFlat = new List<float>();
        existingStats.m_dmgReduceFlat = new List<float>();
        existingStats.m_dmgReducePercent = new List<float>();

        return existingStats;
    }
}
