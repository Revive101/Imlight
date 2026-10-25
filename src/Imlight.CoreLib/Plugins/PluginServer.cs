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
 * PLUGIN SERVER
 * ========================================================================
 *
 * PURPOSE:
 * Exposes the server to plugins as an IServer, built from the running
 * zones' player snapshots.
 *
 * NOTE:
 * A player changing zones can briefly sit in both zones' snapshots, as
 * each zone actor updates its own, so Players keeps one per character.
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 10/24/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Exposes the server to plugins as an <see cref="IServer"/>.
/// </summary>
internal sealed class PluginServer : IServer {

    public IReadOnlyList<IZone> Zones => [.. PluginZone.All];

    public IReadOnlyList<IPlayer> Players
        => [.. PluginZone.All.SelectMany(x => x.Players).DistinctBy(x => x.CharacterId)];

    public IPlayer GetPlayer(ulong characterId)
        => PluginZone.All.SelectMany(x => x.Players).FirstOrDefault(x => x.CharacterId == characterId);

    public IPlayer GetPlayer(string name)
        => PluginZone.All.SelectMany(x => x.Players)
            .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Broadcast(string message) {
        foreach (var player in Players) {
            player.SendMessage(message);
        }
    }

}
