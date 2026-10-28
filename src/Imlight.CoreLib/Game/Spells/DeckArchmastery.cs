/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * DECK ARCHMASTERY SCHOOL
 * ========================================================================
 *
 * PURPOSE:
 * Stores the archmastery school a player picks for a deck (MSG_SetDeckArchmastery), on the live item
 * and in the database, so the next duel starts converting to that school.
 *
 * USAGE EXAMPLE:
 * DeckArchmastery.SetSchool(wizard, message.ItemGID, message.School);
 *
 * NOTE:
 * The client sends no ItemGID when the pick comes from the combat nameplate; the equipped deck is meant.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Linq;

namespace Imlight.CoreLib.Game.Spells;

/// <summary>
/// The archmastery school saved on a player's deck.
/// </summary>
internal static class DeckArchmastery {

    /// <summary>
    /// Saves the school on the given deck, or on the equipped deck when no deck is named.
    /// </summary>
    internal static void SetSchool(Wizard wizard, ulong deckId, uint school) {
        if (wizard is null) {
            return;
        }

        if (deckId == 0) {
            deckId = wizard.EquipmentBehavior?.SlotList?
                .FirstOrDefault(s => s.SlotType == EquipmentSlotType.Deck)?.ItemId ?? 0;
        }

        if (deckId == 0) {
            return;
        }

        var deckItem = wizard.InventoryBehavior?.Items?.FirstOrDefault(i => i.m_globalID == deckId)
                       ?? wizard.EquipmentBehavior?.EquippedItems?.FirstOrDefault(i => i.m_globalID == deckId);
        if (deckItem is not null && CoreObjectFactory.FindBehaviorInstance<DeckBehavior>(deckItem, out var deck)) {
            deck.m_archmasterySchool = school;
        }

        WizardItemCollection.SetDeckArchmasterySchool(deckId, school);
    }

}
