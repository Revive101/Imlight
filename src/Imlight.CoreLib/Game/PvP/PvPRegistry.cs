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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * Thread-safe, read-only lookup of who is queued or in a match. Only the PvPDirector writes here; everything
 * else reads.
 * 
 * USAGE EXAMPLE:
 * if (PvPRegistry.IsQueued(wizard.CharId)) { }
 * 
 * NOTE:
 * Readers may observe a state that is one change behind the director. Anything that must be exact goes through
 * the director actor instead.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.PvP;

internal static class PvPRegistry {

    private static readonly ConcurrentDictionary<ulong, byte> s_queued = new();
    private static readonly ConcurrentDictionary<ulong, byte> s_inMatch = new();
    private static readonly ConcurrentDictionary<ulong, byte> s_matchInstanceOwners = new();
    private static readonly ConcurrentDictionary<ulong, byte> s_hosting = new();
    private static readonly ConcurrentDictionary<ulong, PvPMatchInfo> s_hostedMatches = new();

    public static bool IsQueued(ulong charId)
        => s_queued.ContainsKey(charId);

    public static bool IsHosting(ulong charId)
        => s_hosting.ContainsKey(charId);

    public static bool IsInMatch(ulong charId)
        => s_inMatch.ContainsKey(charId);

    public static IReadOnlyList<PvPMatchInfo> GetHostedMatches(PvPListScope scope)
        => [.. s_hostedMatches.Values.Where(scope.Includes).OrderBy(info => (ulong) info.m_matchID)];

    public static bool IsMatchInstanceOwner(ulong ownerId)
        => s_matchInstanceOwners.ContainsKey(ownerId);

    internal static void SetQueued(ulong charId, bool isQueued) {
        if (isQueued) {
            s_queued[charId] = 0;
        } else {
            s_queued.TryRemove(charId, out _);
        }
    }

    internal static void SetHosting(ulong charId, bool isHosting) {
        if (isHosting) {
            s_hosting[charId] = 0;
        } else {
            s_hosting.TryRemove(charId, out _);
        }
    }

    internal static void SetHostedMatch(ulong matchId, PvPMatchInfo info) {
        if (info is not null) {
            s_hostedMatches[matchId] = info;
        } else {
            s_hostedMatches.TryRemove(matchId, out _);
        }
    }

    internal static void SetInMatch(ulong charId, bool isInMatch) {
        if (isInMatch) {
            s_inMatch[charId] = 0;
        } else {
            s_inMatch.TryRemove(charId, out _);
        }
    }

    internal static void SetMatchInstanceOwner(ulong ownerId, bool isOwner) {
        if (isOwner) {
            s_matchInstanceOwners[ownerId] = 0;
        } else {
            s_matchInstanceOwners.TryRemove(ownerId, out _);
        }
    }

}
