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

using System.Collections.Generic;

namespace Imlight.CoreLib.WizardData.Models.World;

public class TreasureCardEntry {

    /// <summary>
    /// The spell name of the treasure card (e.g. "Fireblade", "Pixie").
    /// Must match the m_name field of a SpellTemplate in the WAD.
    /// </summary>
    public string SpellName { get; set; }

    /// <summary>
    /// Gold price to buy this treasure card from this vendor.
    /// If 0, falls back to the spell template's m_baseCost.
    /// </summary>
    public int Price { get; set; }

}

public class NpcTreasureCardInventory {

    /// <summary>
    /// The NPC template ID this inventory belongs to.
    /// </summary>
    public ulong TemplateID { get; set; }

    /// <summary>
    /// The treasure cards this NPC sells.
    /// </summary>
    public List<TreasureCardEntry> TreasureCards { get; set; } = [];

}
