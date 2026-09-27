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
 * BUDDY STATS CRC
 * ========================================================================
 * 
 * PURPOSE:
 * Reproduces the checksums the game client puts in a MSG_BUDDYSTATS request for the
 * blocks it has cached, so the server can leave out blocks the client already holds.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * The checksum is CRC-32 (reflected 0xEDB88320, initial value 0, no final XOR), but each
 * block feeds it a different input. Stats and non-empty equipment are never predictable:
 * the client serializes them with uninitialized padding bits.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Cryptography;

namespace Imlight.CoreLib.Shared.Character;

internal static class BuddyStatsCrc {

    internal const uint EmptyEquipmentList = 0x0795908D;

    internal static uint Compute(byte[] data)
        => Crc32.Calculate(0, data);

    internal static uint ForCharacter(byte[] avatarObject) {
        // The client's flag byte keeps bits 1..7 of the object's first byte from its reused buffer.
        var block = BuddyStatsBuilder.WrapBlock(avatarObject, BuddyStatsBuilder.BlockSerializerFlags, avatarObject[0]);

        return Compute(block);
    }

    internal static uint ForEffects(IEnumerable<GameEffectBase> publicEffects) {
        var crc = 0u;
        foreach (var effect in publicEffects) {
            if (effect is not null) {
                crc ^= effect.m_effectNameID;
            }
        }

        return crc;
    }

    internal static uint ForPet(BasePetItemBehavior pet) {
        uint crc = pet.m_level;
        foreach (var stat in pet.m_currentStats ?? []) {
            if (stat is not null) {
                crc ^= stat.m_statID ^ (uint) stat.m_value;
            }
        }

        return crc;
    }

    internal static bool TryForEquipment(ICollection<EquippedItemInfo> publicItems, out uint crc) {
        // Each item's color bit fields leave a padding bit the client never initializes.
        crc = EmptyEquipmentList;

        return publicItems is null || publicItems.Count == 0;
    }

    internal static uint ForWishlist(byte[] wishlistData)
        => Compute(wishlistData);

}
