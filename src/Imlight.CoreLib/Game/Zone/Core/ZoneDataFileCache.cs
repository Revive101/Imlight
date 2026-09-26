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
 * ZONE LOADING SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Keeps the raw bytes of the six data files a zone load reads, so loading the same zone again
 * (another instance, a reload after the zone closed) skips opening its archive.
 * 
 * USAGE EXAMPLE:
 * if (!ZoneDataFileCache.TryGet(zonePath, out var files)) {
 *     files = ReadFromArchive(zonePath);
 *     ZoneDataFileCache.Add(zonePath, files);
 * }
 * 
 * NOTE:
 * Only bytes are cached: every load deserializes its own objects, because zone code mutates the
 * loaded data (trigger results, spawn info). Archives do not change while the server runs.
 * Least recently used zones are evicted beyond the capacity.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Collections.Generic;
using System.Threading;

namespace Imlight.CoreLib.Game.Zone.Core;

internal sealed record ZoneDataFiles(
    byte[] ZoneData,
    byte[] SpawnData,
    byte[] PathData,
    byte[] NodeData,
    byte[] VolumeData,
    byte[] TriggerData);

internal static class ZoneDataFileCache {

    private const int Capacity = 64;

    private static readonly Lock s_lock = new();
    private static readonly Dictionary<string, LinkedListNode<(string ZonePath, ZoneDataFiles Files)>> s_entries
        = new(StringComparer.Ordinal);
    private static readonly LinkedList<(string ZonePath, ZoneDataFiles Files)> s_recentlyUsed = new();

    public static bool TryGet(string zonePath, out ZoneDataFiles files) {
        lock (s_lock) {
            if (!s_entries.TryGetValue(zonePath, out var node)) {
                files = null;

                return false;
            }

            s_recentlyUsed.Remove(node);
            s_recentlyUsed.AddFirst(node);
            files = node.Value.Files;

            return true;
        }
    }

    public static void Add(string zonePath, ZoneDataFiles files) {
        lock (s_lock) {
            if (s_entries.TryGetValue(zonePath, out var existing)) {
                s_recentlyUsed.Remove(existing);
            }

            s_entries[zonePath] = s_recentlyUsed.AddFirst((zonePath, files));

            while (s_entries.Count > Capacity) {
                var oldest = s_recentlyUsed.Last;
                s_recentlyUsed.RemoveLast();
                s_entries.Remove(oldest.Value.ZonePath);
            }
        }
    }

}
