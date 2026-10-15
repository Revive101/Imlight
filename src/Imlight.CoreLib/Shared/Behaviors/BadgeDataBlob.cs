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
 * BADGE DATA BLOB
 * ========================================================================
 *
 * PURPOSE:
 * Encodes and decodes the m_badgeData string of the client's BadgeBehavior: the lists of claimed rewards, new badges and pending overcount rewards.
 *
 * USAGE EXAMPLE:
 * var payload = BadgeDataBlob.Encode(claimedRewards, newBadges, pendingOvercount);
 *
 * NOTE:
 * The payload is little-endian: magic 0x2ACEFACE, version 1, then three lists, each a u16 count followed by its entries. The u16 length in front of the whole payload is written by the property serializer.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.IO;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Behaviors;

internal static class BadgeDataBlob {

    internal const uint Magic = 0x2ACEFACE;
    internal const uint Version = 1;

    internal static byte[] Encode(IReadOnlyCollection<uint> claimedRewards, IReadOnlyCollection<NewBadgeEntry> newBadges, IReadOnlyCollection<uint> pendingOvercount) {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Magic);
        writer.Write(Version);

        writer.Write((ushort) Math.Min(claimedRewards.Count, ushort.MaxValue));
        foreach (var id in claimedRewards) {
            writer.Write(id);
        }

        writer.Write((ushort) Math.Min(newBadges.Count, ushort.MaxValue));
        foreach (var entry in newBadges) {
            writer.Write(entry.BadgeId);
            writer.Write(entry.Time);
        }

        writer.Write((ushort) Math.Min(pendingOvercount.Count, ushort.MaxValue));
        foreach (var id in pendingOvercount) {
            writer.Write(id);
        }

        writer.Flush();

        return stream.ToArray();
    }

    internal static bool TryDecode(byte[] payload, out List<uint> claimedRewards, out List<NewBadgeEntry> newBadges, out List<uint> pendingOvercount) {
        claimedRewards = [];
        newBadges = [];
        pendingOvercount = [];

        try {
            using var reader = new BinaryReader(new MemoryStream(payload));
            if (reader.ReadUInt32() != Magic || reader.ReadUInt32() != Version) {
                return false;
            }

            for (var left = reader.ReadUInt16(); left > 0; left--) {
                claimedRewards.Add(reader.ReadUInt32());
            }

            for (var left = reader.ReadUInt16(); left > 0; left--) {
                newBadges.Add(new NewBadgeEntry { BadgeId = reader.ReadUInt32(), Time = reader.ReadUInt32() });
            }

            for (var left = reader.ReadUInt16(); left > 0; left--) {
                pendingOvercount.Add(reader.ReadUInt32());
            }

            return reader.BaseStream.Position == reader.BaseStream.Length;
        } catch (EndOfStreamException) {
            return false;
        }
    }

}
