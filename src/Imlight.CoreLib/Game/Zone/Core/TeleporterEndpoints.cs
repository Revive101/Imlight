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
 * TELEPORTER ENDPOINTS
 * ========================================================================
 * 
 * PURPOSE:
 * The discoverable teleporter endpoints of one zone (stones and pads such as Marleybone's, each named by
 * its template): where each stands, where it sends a player, which endpoint is its partner, and which
 * players have discovered it. A link works for a player only once both of its endpoints are discovered.
 * 
 * USAGE EXAMPLE:
 * zone.TeleporterEndpoints.Register("MB_TeleportStoneBMain", position, destination);
 * var partner = zone.TeleporterEndpoints.FindPartner("MB_TeleportStoneBMain");
 * 
 * NOTE:
 * A discovery is saved as a registry entry on the character (EntryName); the in-memory set covers the
 * moment between a discovery and its entry being written. Endpoints register when their zone loads.
 * The partner is the endpoint each one lands the player beside: the hub stones share one arrival point,
 * so the pair is the one whose destinations both fall beside the other end.
 * 
 * TODO:
 * 
 * Created by: WonderlandWorkshop
 * Version: KALI 1.0
 * Last Updated: 11/12/2026
 */

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Core;

public sealed class TeleporterEndpoints {

    private const string ENTRY_PREFIX = "TeleporterFound";

    private sealed record Endpoint(string Name, Vector2 Position, Vector2 Destination);

    private readonly ConcurrentDictionary<string, Endpoint> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _found = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, Vector2 position, Vector2 destination) {
        if (!string.IsNullOrEmpty(name)) {
            _endpoints[name] = new Endpoint(name, position, destination);
        }
    }

    /// <summary>
    /// The endpoint paired with <paramref name="name"/>: the one that lands a player beside it while it lands
    /// the player beside that one. Null when the zone holds no other endpoint.
    /// </summary>
    public string FindPartner(string name) {
        if (!_endpoints.TryGetValue(name ?? string.Empty, out var self)) {
            return null;
        }

        return _endpoints.Values
            .Where(other => !string.Equals(other.Name, self.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(other => Vector2.Distance(other.Destination, self.Position) + Vector2.Distance(self.Destination, other.Position))
            .FirstOrDefault()?.Name;
    }

    public bool IsFound(Wizard wizard, string zonePath, string name)
        => wizard is not null
        && (_found.ContainsKey(FoundKey(wizard.CharId, name)) || wizard.HasRegistryValue(EntryName(zonePath, name)));

    /// <summary>
    /// Records the discovery for this session. False when it was already recorded.
    /// </summary>
    public bool MarkFound(ulong charId, string name)
        => _found.TryAdd(FoundKey(charId, name), 0);

    /// <summary>
    /// The registry entry a character keeps for a discovered endpoint. Templates repeat across zones
    /// (MB_TeleportStoneDMain stands in four Marleybone zones), so the zone is part of the name.
    /// </summary>
    public static string EntryName(string zonePath, string name)
        => $"{ENTRY_PREFIX}:{zonePath}:{name}";

    private static string FoundKey(ulong charId, string name)
        => $"{charId}|{name}";

}
