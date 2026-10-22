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
 * HOUSING SYSTEM
 * ========================================================================
 *
 * PURPOSE:
 * One blob's slot table: the items in slot order and the base the client numbers its entries from.
 *
 * USAGE EXAMPLE:
 * var table = new BlobTable<PlacedItem>();
 * var gidId = table.GidIdOf(slot);
 *
 * NOTE:
 * - A removed item leaves a null slot, so the numbers in later patches stay valid.
 * - The table is not thread-safe; its owner changes it under its own lock.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Housing.Items;

internal sealed class BlobTable<TItem> where TItem : class {

    /// <summary>
    /// The items in slot order; a removed slot is <c>null</c>.
    /// </summary>
    internal List<TItem> Slots { get; set; } = [];

    /// <summary>
    /// The base the entries of the blob are numbered from.
    /// </summary>
    internal uint GidBase { get; set; }

    /// <summary>
    /// The number of items in the table, not counting removed slots.
    /// </summary>
    internal int Live => Slots.Count(item => item is not null);

    /// <summary>
    /// Gets the patch <c>GIDID</c> of a slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The base plus the slot.</returns>
    internal uint GidIdOf(int slot)
        => unchecked(GidBase + (uint) slot);

}
