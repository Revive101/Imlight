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
 * The players waiting for a match. Plain state owned by the PvPDirector; only the director's thread uses it.
 * 
 * USAGE EXAMPLE:
 * if (queue.Add(entry)) { }
 * 
 * NOTE:
 * 
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.Game.PvP.Matchmaking;

internal sealed class PvPQueue {

    private readonly Dictionary<ulong, PvPQueueEntry> _entries = [];

    public int Count => _entries.Count;
    public IEnumerable<PvPQueueEntry> Entries => _entries.Values;

    public bool Contains(ulong charId)
        => _entries.ContainsKey(charId);

    public bool TryGet(ulong charId, out PvPQueueEntry entry)
        => _entries.TryGetValue(charId, out entry);

    public bool Add(PvPQueueEntry entry)
        => _entries.TryAdd(entry.CharId, entry);

    public bool Remove(ulong charId)
        => _entries.Remove(charId);

    public void Update(PvPQueueEntry entry)
        => _entries[entry.CharId] = entry;

}
