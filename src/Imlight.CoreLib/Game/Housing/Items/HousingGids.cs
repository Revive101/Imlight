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
 * The arithmetic of the ids the client gives the entries of a housing blob, shared by the blob of a house and the
 * blobs of an attic.
 *
 * USAGE EXAMPLE:
 * var gidBase = HousingGids.NewBase();
 * var gidId = unchecked(gidBase + (uint) slot);
 * if (HousingGids.TryGetSlot(objectGid, gidBase, slotCount, out var slot)) { ... }
 *
 * NOTE:
 * - The client numbers the entries of a blob base, base + 1, and so on, and builds the object id
 *   (u16 << 48) | (9 << 40) | gidId. A removed entry keeps its number.
 * - The base is drawn so that base + MAX_SLOTS never wraps past 2^32.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.Types;

namespace Imlight.CoreLib.Game.Housing.Items;

internal static class HousingGids {

    /// <summary>
    /// The most bytes one blob holds: a string field is at most 65,535 bytes.
    /// </summary>
    internal const int MAX_BLOB_BYTES = 65535;

    /// <summary>
    /// The bytes of a blob without entries.
    /// </summary>
    internal const int BLOB_HEADER_BYTES = 47;

    /// <summary>
    /// The bytes of a plain entry. A custom entry adds the length of its string.
    /// </summary>
    internal const int ENTRY_BYTES = 21;

    /// <summary>
    /// The most plain entries one blob holds. A kind blob holds fewer, because its entries carry a string.
    /// </summary>
    internal const int MAX_SLOTS = (MAX_BLOB_BYTES - BLOB_HEADER_BYTES) / ENTRY_BYTES;

    private const int OBJECT_GID_TYPE = 9;
    private const long LOWEST_BASE = 0x80000000L;
    private const long HIGHEST_BASE = 0xFFFF0000L;

    /// <summary>
    /// Checks whether a blob of a number of entries, with a total of custom string bytes, fits a string field.
    /// </summary>
    /// <param name="entries">The entries, removed slots included.</param>
    /// <param name="customBytes">The bytes of all custom strings.</param>
    /// <returns><c>true</c> when the serialized blob stays within <see cref="MAX_BLOB_BYTES"/>.</returns>
    internal static bool Fits(int entries, int customBytes)
        => (long) BLOB_HEADER_BYTES + (long) ENTRY_BYTES * entries + customBytes <= MAX_BLOB_BYTES;

    /// <summary>
    /// Draws the base the client numbers a blob's entries from. The range leaves room for <see cref="MAX_SLOTS"/> entries
    /// above any base without wrapping.
    /// </summary>
    /// <returns>A new base.</returns>
    internal static uint NewBase()
        => (uint) Random.Shared.NextInt64(LOWEST_BASE, HIGHEST_BASE);

    /// <summary>
    /// Draws a base whose entries cannot share an id with the entries of the other bases.
    /// </summary>
    /// <param name="others">The bases already in use.</param>
    /// <returns>A new base at least <see cref="MAX_SLOTS"/> away from every other.</returns>
    internal static uint NewBaseApart(IReadOnlyCollection<uint> others) {
        while (true) {
            var candidate = NewBase();
            if (others.All(other => candidate + (long) MAX_SLOTS <= other || other + (long) MAX_SLOTS <= candidate)) {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Finds the slot an object id names. The client builds the id as <c>(u16 &lt;&lt; 48) | (9 &lt;&lt; 40) | gidId</c>.
    /// </summary>
    /// <param name="gid">The object id the client sent.</param>
    /// <param name="gidBase">The base of the blob.</param>
    /// <param name="slotCount">The number of slots the blob has, removed ones included.</param>
    /// <param name="slot">The slot when this returns <c>true</c>.</param>
    /// <returns><c>true</c> when the id is a housing object id inside the blob's numbering.</returns>
    internal static bool TryGetSlot(ulong gid, uint gidBase, int slotCount, out int slot) {
        slot = -1;
        var parts = new GID(gid).MParts;
        if (parts.Type != OBJECT_GID_TYPE || parts.Block != 0) {
            return false;
        }

        var offset = unchecked(parts.TemplateId - gidBase);
        if (offset >= (uint) slotCount) {
            return false;
        }

        slot = (int) offset;

        return true;
    }

}
