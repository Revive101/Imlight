/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.Common.Utilities;
using Imlight.CoreLib.Game.Effects;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Shared.Character;

public static class CharacterHelper {
    internal const float OrientationCompressionFactor = 0.708f;

    /// <summary>
    /// Recalculates the game stats for a wizard.
    /// </summary>
    /// <param name="wizard">The wizard whose game stats need to be recalculated.</param>
    internal static void RecalculateGameStats(Wizard wizard) {
        Logger.Debug("Recalculation of game stats for {0}.", Logger.Args(wizard.PlayerNameBehavior.GetWizardName()));

        // Reset the base stats to the default values.
        wizard.GameStats.SetBaseStats();
        wizard.GameEffects = new List<GameEffectBase>();

        // Iterate through the equipped items and apply their effects.
        foreach (var item in wizard.EquipmentBehavior.EquippedItems) {
            var template = ItemHelper.GetItemTemplate(item);
            if (template is null) {
                continue;
            }

            var activatedEffects = CharacterEffectHelper.AddEffectsToWizard(wizard, template);

            Logger.Debug("{0} Applied {1} effects for item {2}.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), activatedEffects.Count, template.m_objectName));
        }

        Logger.Debug("Game stats recalculated for {0}.", Logger.Args(wizard.PlayerNameBehavior.GetWizardName()));
    }

    /// <summary>
    /// Creates a character from the character creation screen.
    /// </summary>
    /// <param name="creationInfo">The character creation information.</param>
    /// <returns>The created Wizard character.</returns>
    public static Wizard CreateCharacterFromCreationInfo(WizardCharacterCreationInfo creationInfo) {
        // This method is used to create a character from the character creation screen.
        var school = (MagicSchool) creationInfo.m_schoolOfFocus;
        var wizardAvatar = creationInfo.m_avatarBehavior;
        var nameIndices = creationInfo.m_nameIndices;
        var character = new Wizard(school, wizardAvatar, nameIndices);

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
            m_nameIndices = character.PlayerNameBehavior.NameIndices,
            m_schoolOfFocus = (uint) character.MagicSchoolBehavior.MagicSchool,
            m_level = character.MagicSchoolBehavior.Level,
            m_name = character.PlayerNameBehavior.NameOverride,
            m_location = character.ZoneDisplayName,
            m_globalID = (GID) character.CharId,
            m_templateID = 1,
            m_userID = (GID) character.AccountId,
            m_equipmentInfoList = GetEquipmentList(character.EquipmentBehavior),
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
        var sortedList = new EquippedItemInfoList {
            m_infoList = new List<EquippedItemInfo>(),
        };

        foreach (var slot in behavior.SlotList) {
            var equippedItem = behavior.EquippedItems.FirstOrDefault(item => item.m_globalID == slot.ItemId);
            if (equippedItem != null) {
                var publicItem = ItemHelper.GetPublicItem(equippedItem);
                sortedList.m_infoList.Add(publicItem);
            }
        }

        // Sorting the list based on the order in which they appear in the SlotList
        sortedList.m_infoList.Sort((item1, item2) => {
            var index1 = behavior.SlotList.FindIndex(slot => slot.ItemId == item1.m_itemID);
            var index2 = behavior.SlotList.FindIndex(slot => slot.ItemId == item2.m_itemID);
            return index1.CompareTo(index2);
        });

        return sortedList;
    }
}
