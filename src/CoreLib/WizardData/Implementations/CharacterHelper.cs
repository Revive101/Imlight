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
        var gameStats = new WizGameStats();
        gameStats = SetCharacterStatsToBase(gameStats, character.Level, character.WizardSchool);
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
    internal static EquippedItemInfoList GetEquipmentList(Wizard character) {
        var equipmentList = new EquippedItemInfoList {
            m_infoList = new List<EquippedItemInfo>(),
        };
        foreach (var equippedItem in character.EquippedItems.Where(x => x.m_itemID != 0)) {
            // For every equipped item, get the actual item from the inventory.
            // Then, create a new WizardEquippedItemInfo from the actual item.
            // This is a smaller version of the item that is used for the character select screen.
            var itemId = equippedItem.m_itemID;
            var actualItem = character.InventoryGetItem(itemId)
                ?? throw new Exception($"Could not find item with ID {itemId} in inventory.");
            var publicItem = ItemHelper.GetPublicItem(actualItem);

            equipmentList.m_infoList.Add(publicItem);
        }

        return equipmentList;
    }

    internal static void AddGameEffectToStats(WizGameStats stats, WizStatisticEffect statistic) {
        // Given a statistic, add it to the stats.
        stats.m_baseHitpoints += (int) statistic.m_hitPointBonus;
        stats.m_baseMana += (int) statistic.m_manaBonus;
        stats.m_energyMax += (int) statistic.m_energyBonus;
        stats.m_iceMastery = statistic.m_iceMastery;
        stats.m_fireMastery = statistic.m_fireMastery;
        stats.m_stormMastery = statistic.m_stormMastery;
        stats.m_mythMastery = statistic.m_mythMastery;
        stats.m_lifeMastery = statistic.m_lifeMastery;
        stats.m_deathMastery = statistic.m_deathMastery;
        stats.m_balanceMastery = statistic.m_balanceMastery;
        stats.m_powerPipBonusPercentAll += statistic.m_powerPipBonusPercent;
    }

    private static WizGameStats SetCharacterStatsToBase(WizGameStats existingStats, byte level, MagicSchool school) {
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
