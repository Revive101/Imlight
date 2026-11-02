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
 * Last Updated: 11/01/2026
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
    /// The shared zone with this internal name, matched case-insensitively, or null if it is not loaded.
    /// </summary>
    /// <remarks>
    /// Instances are not included, as several can share a name. Find those through <see cref="Zones"/>.
    /// </remarks>
    IZone? GetZone(string name);

    /// <summary>
    /// The creature with this global ID, or null if it is not spawned.
    /// </summary>
    ICreature? GetCreature(ulong globalId);

    /// <summary>
    /// The item with this template ID, or null if the ID does not name an item.
    /// </summary>
    /// <remarks>
    /// The first lookup of a template reads it from the game files, so avoid looking up many at once on a game event.
    /// </remarks>
    ItemInfo? GetItem(ulong templateId);

    /// <summary>
    /// Every item with this internal name, matched case-insensitively. The list is empty if there are none.
    /// </summary>
    /// <remarks>
    /// English names cannot be searched, as finding one would mean reading every template in the game files.
    /// </remarks>
    IReadOnlyList<ItemInfo> FindItems(string internalName);

    /// <summary>
    /// The spell with this template ID, or null if the ID does not name a spell.
    /// </summary>
    SpellInfo? GetSpell(uint templateId);

    /// <summary>
    /// The spell with this internal name, or null if there is none. The name must match exactly,
    /// including its case.
    /// </summary>
    SpellInfo? GetSpell(string name);

    /// <summary>
    /// Shows a server message to every player in a zone. Safe to call from any thread.
    /// </summary>
    void Broadcast(string message);

}
