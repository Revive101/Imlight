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
 * ZONE INSTANCE OWNERS
 * ========================================================================
 * 
 * PURPOSE:
 * Tracks which instance container a character is sent to when they attach to a zone,
 * and which zones are always attached privately.
 * 
 * USAGE EXAMPLE:
 * ZoneInstanceOwners.Assign(charId, "Arena/Duel", matchOwnerId);
 * ZoneInstanceOwners.RegisterPrivateZoneTest(zoneName => zoneName.StartsWith("Housing/"));
 * 
 * NOTE:
 * - In memory only: an assignment cannot outlive the session that made it.
 * - Read by AttachService, because the client attaches again after every zone transfer. A house overrides it: the
 *   attach always lands in the owner's container, so the lot decides and nothing is assigned for a visit.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Imlight.CoreLib.Game.World;

/// <summary>
/// Maps a character and zone name to the owner of the instance that character attaches to.
/// </summary>
internal static class ZoneInstanceOwners {

    private static readonly ConcurrentDictionary<(ulong CharId, string ZoneName), ulong> s_owners
        = new(new CharacterZoneComparer());
    private static readonly ConcurrentQueue<Func<string, bool>> s_privateZoneTests = new();

    /// <summary>
    /// Sends <paramref name="charId"/> to the instance owned by <paramref name="ownerCharId"/> when they attach to the zone.
    /// </summary>
    /// <param name="charId">The character who attaches.</param>
    /// <param name="zoneName">The zone name, compared without regard to case.</param>
    /// <param name="ownerCharId">The owner of the instance container that holds the zone.</param>
    internal static void Assign(ulong charId, string zoneName, ulong ownerCharId)
        => s_owners[(charId, zoneName)] = ownerCharId;

    /// <summary>
    /// Removes the assignment made by <see cref="Assign"/>, so the character attaches to their own instance again.
    /// </summary>
    /// <param name="charId">The character who attaches.</param>
    /// <param name="zoneName">The zone name, compared without regard to case.</param>
    internal static void Clear(ulong charId, string zoneName)
        => s_owners.TryRemove((charId, zoneName), out _);

    /// <summary>
    /// Gets the instance owner assigned to a character for a zone.
    /// </summary>
    /// <param name="charId">The character who attaches.</param>
    /// <param name="zoneName">The zone name, compared without regard to case.</param>
    /// <param name="ownerCharId">The assigned owner when this returns <c>true</c>.</param>
    /// <returns><c>true</c> when an owner was assigned.</returns>
    internal static bool TryGetOwner(ulong charId, string zoneName, out ulong ownerCharId) {
        ownerCharId = 0;

        return zoneName is not null && s_owners.TryGetValue((charId, zoneName), out ownerCharId);
    }

    /// <summary>
    /// Decides which instance container an attach lands in and whether the zone is private to it. A house is always in
    /// its owner's container, whoever visits; otherwise an assignment decides; otherwise the character's own container.
    /// </summary>
    /// <param name="charId">The character who attaches.</param>
    /// <param name="zoneName">The zone name, compared without regard to case.</param>
    /// <param name="houseOwnerCharId">The owner of the house the character attaches to, or 0 for a zone that is not a house.</param>
    /// <returns>The owner of the container, and whether the zone is private to it.</returns>
    internal static (ulong OwnerCharId, bool IsPrivate) Resolve(ulong charId, string zoneName, ulong houseOwnerCharId) {
        var hasOwner = houseOwnerCharId != 0 || TryGetOwner(charId, zoneName, out houseOwnerCharId);

        return (hasOwner ? houseOwnerCharId : charId, hasOwner || IsAlwaysPrivate(zoneName));
    }

    /// <summary>
    /// Registers a test that marks zones as always attached privately. Register each test once, at startup.
    /// </summary>
    /// <param name="test">Returns <c>true</c> for the zone names it accepts.</param>
    internal static void RegisterPrivateZoneTest(Func<string, bool> test) {
        ArgumentNullException.ThrowIfNull(test);

        s_privateZoneTests.Enqueue(test);
    }

    /// <summary>
    /// Checks whether any registered test accepts the zone name.
    /// </summary>
    /// <param name="zoneName">The zone name to test.</param>
    /// <returns><c>true</c> when the zone is always attached privately.</returns>
    internal static bool IsAlwaysPrivate(string zoneName) {
        if (string.IsNullOrEmpty(zoneName)) {
            return false;
        }

        foreach (var test in s_privateZoneTests) {
            if (test(zoneName)) {
                return true;
            }
        }

        return false;
    }

    private sealed class CharacterZoneComparer : IEqualityComparer<(ulong CharId, string ZoneName)> {

        public bool Equals((ulong CharId, string ZoneName) x, (ulong CharId, string ZoneName) y)
            => x.CharId == y.CharId && string.Equals(x.ZoneName, y.ZoneName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((ulong CharId, string ZoneName) key)
            => HashCode.Combine(key.CharId, StringComparer.OrdinalIgnoreCase.GetHashCode(key.ZoneName));

    }

}
