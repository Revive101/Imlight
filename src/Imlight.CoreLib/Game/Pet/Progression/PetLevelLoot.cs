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
 * PET LEVEL LOOT
 * ========================================================================
 *
 * PURPOSE:
 * Grants the loot tables a pet template names for a level the pet reaches.
 *
 * USAGE EXAMPLE:
 * PetLevelLoot.Grant(session, playerObj, wizard, pet, 4);
 *
 * NOTE:
 * A table that SpiralDB does not have is skipped and logged once, never replaced with invented loot.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Linq;
using System;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Pet.Progression;

/// <summary>
/// Grants the loot a pet earns on reaching a level.
/// </summary>
internal static class PetLevelLoot {

    /// <summary>
    /// Finds the loot tables a pet's template names for a level and SpiralDB has.
    /// </summary>
    /// <param name="pet">The pet item.</param>
    /// <param name="level">The level reached.</param>
    /// <returns>The names of the tables to roll. A name SpiralDB lacks is left out and logged the first time.</returns>
    internal static string[] FindTables(WizClientObjectItem pet, int level) {
        if (!PetProgression.TryGetPet(pet, out _, out var template)) {
            return [];
        }

        var named = template.m_Levels?.FirstOrDefault(info => info.m_level == level)?.m_lootTable ?? [];

        return DropTableRoller.KeepKnownTables(named.Select(table => table.ToString()).Where(table => !string.IsNullOrWhiteSpace(table)));
    }

    /// <summary>
    /// Rolls and grants the loot for a level the pet reached, with the loot popup.
    /// </summary>
    /// <param name="session">The owner's session.</param>
    /// <param name="playerObj">The owner's game object, which the loot requirements are checked against.</param>
    /// <param name="wizard">The owner.</param>
    /// <param name="pet">The pet item.</param>
    /// <param name="level">The level reached.</param>
    internal static void Grant(IActorRef session, CoreObject playerObj, Wizard wizard, WizClientObjectItem pet, int level) {
        var tables = FindTables(pet, level);
        if (tables.Length == 0) {
            return;
        }

        var loot = DropTableRoller.Roll(tables, session, playerObj, wizard);
        LootGranter.GrantAndDisplay(session, wizard, loot);
    }

}
