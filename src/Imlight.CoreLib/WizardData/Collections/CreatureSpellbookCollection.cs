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
*/

using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class CreatureSpellbookCollection {

    private static readonly uint[] s_defaultSpellIds = [
        84361,      // Imp
        2062265892, // Thundersnake
        1496157882, // Frostbeetle
        2143810477, // Scarab
        1731857280, // Dark sprite
        1067010286, // Bloodbat
    ];

    /// <summary>
    /// Retrieves a creature spellbook by deck name.
    /// </summary>
    /// <param name="deckName">The name of the deck.</param>
    /// <returns>The creature spellbook with the specified deck name, or null if not found.</returns>
    public static CreatureSpellbook GetCreatureSpellbook(string deckName) 
        => SpiralDB.GetCreatureSpellbook(deckName);

    /// <summary>
    /// Retrieves the default creature spellbook.
    /// </summary>
    public static CreatureSpellbook GetDefaultCreatureSpellbook()
        => new("Default", s_defaultSpellIds);

    /// <summary>
    /// Preloads all creature spellbooks.
    /// SpiralDB loads all data at boot; this is a no-op kept for API compatibility.
    /// </summary>
    public static void PreloadSpellbooks() { }

}
