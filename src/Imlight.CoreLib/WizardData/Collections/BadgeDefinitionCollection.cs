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
 * BADGE DEFINITION COLLECTION
 * ========================================================================
 *
 * PURPOSE:
 * Static access to the badge definitions SpiralDB loaded.
 *
 * USAGE EXAMPLE:
 * BadgeDefinitionCollection.TryGet(badgeId, out var definition);
 *
 * NOTE:
 * Game code goes through BadgeCatalog, which validates and indexes these records.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.WizardData.Collections;

public static class BadgeDefinitionCollection {

    /// <summary>
    /// Attempts to retrieve a badge definition by its badge ID.
    /// </summary>
    /// <param name="id">The badge ID, the StringHash of the internal badge name.</param>
    /// <param name="definition">The badge definition.</param>
    /// <returns>True if the definition was found, false otherwise.</returns>
    public static bool TryGet(uint id, out BadgeDefinition definition)
        => SpiralDB.TryGetBadgeDefinition(id, out definition);

    /// <summary>
    /// Returns every loaded badge definition.
    /// </summary>
    public static IEnumerable<BadgeDefinition> GetAll()
        => SpiralDB.BadgeDefinitions.Values;

}
