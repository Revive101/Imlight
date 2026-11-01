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

using System;
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
    /// Checks whether one more entry fits the blob's size limit. Custom entries (pets, mounts) are larger than plain ones, so
    /// a kind blob can pass the limit long before it passes the slot count.
    /// </summary>
    /// <param name="custom">The custom string of the entry to add, or <c>null</c> for a plain entry.</param>
    /// <param name="customOf">Gets the custom string of an item already in the table.</param>
    /// <param name="compacted">Whether the table is measured as it would be after its removed slots are numbered away.</param>
    /// <returns><c>true</c> when the blob with the new entry stays within the limit.</returns>
    internal bool Fits(byte[] custom, Func<TItem, byte[]> customOf, bool compacted = false) {
        var customBytes = (custom?.Length ?? 0) + Slots.Where(item => item is not null).Sum(item => customOf(item)?.Length ?? 0);

        return HousingGids.Fits((compacted ? Live : Slots.Count) + 1, customBytes);
    }

    /// <summary>
    /// Gets the patch <c>GIDID</c> of a slot.
    /// </summary>
    /// <param name="slot">The slot.</param>
    /// <returns>The base plus the slot.</returns>
    internal uint GidIdOf(int slot)
        => unchecked(GidBase + (uint) slot);

}
