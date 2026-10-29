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
 * PENDING CHEST GAMES
 * ========================================================================
 *
 * PURPOSE:
 * Hands a silver chest game from the session that clicked the chest to the session that attaches to the
 * minigame zone, keyed by character.
 *
 * USAGE EXAMPLE:
 * if (PendingChestGames.TryAdd(context)) { ... }
 * if (PendingChestGames.TryTake(charId, out var context)) { ... }
 *
 * NOTE:
 * A zone transfer ends the session and the client attaches with a new one, so the services of the
 * clicking session are gone when the minigame zone is entered. The entries are runtime state and are lost
 * with the server; a character's next attach takes its entry out. An entry whose player never arrived is
 * dropped by the next TryAdd once it is older than the arrival window.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Concurrent;

namespace Imlight.CoreLib.Game.Minigames;

internal static class PendingChestGames {

    internal static readonly TimeSpan s_arrivalWindow = TimeSpan.FromMinutes(2);
    private static readonly ConcurrentDictionary<ulong, Entry> s_pending = new();

    private readonly record struct Entry(ChestGameContext Context, DateTime AddedAt);

    internal static bool TryAdd(ChestGameContext context) {
        var now = DateTime.UtcNow;
        foreach (var entry in s_pending) {
            if (now - entry.Value.AddedAt > s_arrivalWindow) {
                s_pending.TryRemove(entry);
            }
        }

        return s_pending.TryAdd(context.CharId, new Entry(context, now));
    }

    internal static bool TryTake(ulong charId, out ChestGameContext context) {
        var taken = s_pending.TryRemove(charId, out var entry);
        context = entry.Context;

        return taken;
    }

}
