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

public static class CharacterHelper {
    public const float OrientationCompressionFactor = 0.708f;

    public static Wizard CreateCharacterFromCreationInfo(WizardCharacterCreationInfo creationInfo) {
        // This method is used to create a character from the character creation screen.
        var school = (MagicSchoolEnum) creationInfo.m_schoolOfFocus;
        var wizardAvatar = creationInfo.m_avatarBehavior;
        var nameIndices = creationInfo.m_nameIndices;
        var character = new Wizard(school, wizardAvatar, nameIndices);

        // Create the game stats and calculate the base stats.
        var gameStats = new WizGameStats();
        gameStats = SetCharacterStatsToBase(gameStats, character.Level, character.WizardSchool);
        character.GameStats = gameStats;

        return character;
    }

    public static WizardCharacterCreationInfo GetLoginScreenInfo(Wizard character) {
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
    public static EquippedItemInfoList GetEquipmentList(Wizard character) {
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
            var characterSelectItem = new WizardEquippedItemInfo {
                m_itemID = (uint) actualItem.m_templateID,
                m_pattern = (Bui5) actualItem.m_pattern,
                m_baseColor = (Bui5) actualItem.m_primaryColor,
                m_trimColor = (Bui5) actualItem.m_secondaryColor,
            };

            equipmentList.m_infoList.Add(characterSelectItem);
        }

        return equipmentList;
    }

    private static WizGameStats SetCharacterStatsToBase(WizGameStats existingStats, byte level, MagicSchoolEnum school) {
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
