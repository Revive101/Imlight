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
 *
 * ========================================================================
 * STATUE IDENTITY
 * ========================================================================
 *
 * PURPOSE:
 * Gives a player-kind statue a global ID that no real player can have.
 *
 * USAGE EXAMPLE:
 * coreObject.m_globalID = StatueIdentity.CreateGid(statue.Id);
 *
 * NOTE:
 * Every generated ID has 0x4 in its top nibble, so a statue ID with 0xF there never collides with one.
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/24/2026
 */

using Imcodec.Types;
using Imlight.CoreLib.Shared.Utilities;

namespace Imlight.CoreLib.Game.Statues;

internal static class StatueIdentity {

    private const string HASH_PREFIX = "statue:";
    private const int TOP_NIBBLE_SHIFT = 60;
    private const ulong STATUE_TOP_NIBBLE = 0xF;
    private const ulong LOW_BITS_MASK = (1UL << TOP_NIBBLE_SHIFT) - 1;

    internal static GID CreateGid(string statueId)
        => new((RandomGen.GenerateHash(HASH_PREFIX + statueId) & LOW_BITS_MASK) | (STATUE_TOP_NIBBLE << TOP_NIBBLE_SHIFT));

    internal static bool IsStatueId(ulong id) => id >> TOP_NIBBLE_SHIFT == STATUE_TOP_NIBBLE;

}
