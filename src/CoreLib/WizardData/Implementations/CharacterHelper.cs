/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.Common.Utilities;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.WizardData.Implementations;

internal static class CharacterHelper {
    internal const float OrientationCompressionFactor = 0.708f;

    /// <summary>
    /// Creates a character from the character creation screen.
    /// </summary>
    /// <param name="creationInfo">The character creation information.</param>
    /// <returns>The created Wizard character.</returns>
    internal static Wizard CreateCharacterFromCreationInfo(WizardCharacterCreationInfo creationInfo) {
        // This method is used to create a character from the character creation screen.
        var school = (MagicSchool) creationInfo.m_schoolOfFocus;
        var wizardAvatar = creationInfo.m_avatarBehavior;
        var nameIndices = creationInfo.m_nameIndices;
        var character = new Wizard(school, wizardAvatar, nameIndices);

        // Create the game stats and calculate the base stats.
        var gameStats = GetNewCharacterGameStats(character.Level, character.WizardSchool);
        character.GameStats = gameStats;

        return character;
    }

    /// <summary>
    /// Gets the character creation info for an existing <see cref="Wizard"/>.
    /// </summary>
    /// <param name="character">The Wizard object.</param>
    /// <returns>The WizardCharacterCreationInfo for the given Wizard.</returns>
    internal static WizardCharacterCreationInfo GetLoginScreenInfo(Wizard character) {
        var creationInfo = new WizardCharacterCreationInfo {
            m_avatarBehavior = character.WizardAvatar,
            m_nameIndices = character.NameIndices,
            m_schoolOfFocus = (uint) character.WizardSchool,
            m_level = character.Level,
            m_name = character.NameOverride,
            m_location = character.ZoneDisplayName,
            m_globalID = (GID) character.CharId,
            m_templateID = 1,
            m_userID = (GID) character.AccountId,
            m_equipmentInfoList = GetEquipmentList(character),
        };
        return creationInfo;
    }

    /// <summary>
    /// Gets the <see cref="EquippedItemInfoList"/> for a <see cref="Wizard"/>. This is a lightweight version of the
    /// actual equipment that is used to publicly display the character's equipment.
    /// </summary>
    /// <param name="character">The Wizard in question.</param>
    /// <returns>The EquippedItemInfoList that was crafted.</returns>
    /// <exception cref="Exception"></exception>
    internal static EquippedItemInfoList GetEquipmentList(ServerWizEquipmentBehavior behavior) {
        var equipmentList = new EquippedItemInfoList {
            m_infoList = new List<EquippedItemInfo>(),
        };
        foreach (var equippedItem in behavior.EquippedItems) {
            var publicItem = ItemHelper.GetPublicItem(equippedItem);

            equipmentList.m_infoList.Add(publicItem);
        }

        return equipmentList;
    }

    /// <summary>
    /// Resets the stats of a WizGameStats object based on the provided level and magic school.
    /// </summary>
    /// <param name="stats">The WizGameStats object to reset.</param>
    /// <param name="level">The level of the wizard.</param>
    /// <param name="school">The magic school of the wizard.</param>
    internal static void ResetStats(WizGameStats stats, byte level, MagicSchool school) {
        var baseHealth = WizardClassData.GetClassHealthAtLevel(school, level);
        var baseMana = WizardClassData.GetManaAtLevel(level);
        var powerPipChance  = WizardClassData.GetPowerPipChanceAtLevel(level);
        var energyMax = WizardClassData.GetPetEnergyAtLevel(level);

        stats.m_baseHitpoints = baseHealth;
        stats.m_baseMana = baseMana;
        stats.m_baseGoldPouch = ConfigurationManager.Settings.BaseGoldPouch;
        stats.m_powerPipBase = powerPipChance;
        stats.m_energyMax = energyMax;

        // Set all the stats to 0.
        stats.m_dmgBonusFlatAll = 0;
        stats.m_dmgBonusPercentAll = 0;
        stats.m_accBonusPercentAll = 0;
        stats.m_dmgReduceFlatAll = 0;
        stats.m_dmgReducePercentAll = 0;
        stats.m_blockPercentBySchool = new List<float>();
        stats.m_blockRatingBySchool = new List<float>();
        stats.m_dmgBonusFlat = new List<float>();
        stats.m_dmgBonusPercent = new List<float>();
        stats.m_dmgBonusFlat = new List<float>();
        stats.m_dmgReduceFlat = new List<float>();
        stats.m_dmgReducePercent = new List<float>();
        stats.m_accBonusPercent = new List<float>();
        stats.m_blockPercentBySchool = new List<float>();
        stats.m_blockRatingBySchool = new List<float>();
    }

    private static WizGameStats GetNewCharacterGameStats(byte level, MagicSchool school) {
        var stats = new WizGameStats();

        var baseHealth = WizardClassData.GetClassHealthAtLevel(school, level);
        var baseMana = WizardClassData.GetManaAtLevel(level);

        stats.m_baseHitpoints = baseHealth;
        stats.m_currentHitpoints = baseHealth;
        stats.m_baseMana = baseMana;
        stats.m_currentMana = baseMana;
        stats.m_baseGoldPouch = ConfigurationManager.Settings.BaseGoldPouch;
        stats.m_powerPipBase = WizardClassData.GetPowerPipChanceAtLevel(level);
        stats.m_energyMax = WizardClassData.GetPetEnergyAtLevel(level);

        return stats;
    }
}
