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
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 10/24/2026
 */

using System.Collections.Generic;

namespace Imlight.PluginApi;

/// <summary>
/// The game server as a whole.
/// </summary>
public interface IServer {

    /// <summary>
    /// Every zone loaded right now, including each running dungeon instance. Safe to read from any thread;
    /// the list is a snapshot and does not change after it is returned.
    /// </summary>
    IReadOnlyList<IZone> Zones { get; }

    /// <summary>
    /// Every player in a zone right now. Safe to read from any thread; the list is a snapshot
    /// and does not change after it is returned.
    /// </summary>
    /// <remarks>
    /// Players between zones, such as mid zone change or still logging in, are not included.
    /// </remarks>
    IReadOnlyList<IPlayer> Players { get; }

    /// <summary>
    /// The player with this character ID, or null if they are not in a zone.
    /// </summary>
    IPlayer? GetPlayer(ulong characterId);

    /// <summary>
    /// The player with this display name, matched case-insensitively, or null if they are not in a zone.
    /// </summary>
    IPlayer? GetPlayer(string name);

    /// <summary>
    /// Shows a server message to every player in a zone. Safe to call from any thread.
    /// </summary>
    void Broadcast(string message);

}
