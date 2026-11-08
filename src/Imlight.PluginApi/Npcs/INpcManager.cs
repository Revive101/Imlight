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
 * Last Updated: 11/08/2026
 */

using System.Collections.Generic;
using System.Numerics;

namespace Imlight.PluginApi.Npcs;

/// <summary>
/// Spawns this plugin's NPCs and keeps track of them.
/// </summary>
public interface INpcManager {

    /// <summary>
    /// This plugin's NPCs that have not been removed. The list is a snapshot and does not change after it is returned.
    /// </summary>
    IReadOnlyList<INpc> Spawned { get; }

    /// <summary>
    /// Spawns an NPC that looks like the template, such as a placed NPC's template, and shows it to every player in
    /// the zone. It appears once the zone has finished loading.
    /// </summary>
    /// <param name="zone">The zone to spawn it in.</param>
    /// <param name="templateId">The game object template it is built from.</param>
    /// <param name="location">Where it stands.</param>
    /// <param name="orientation">The direction it faces, in radians.</param>
    /// <param name="name">The name shown when players interact with it, or null for the template's name. The client
    /// keeps the template's name over its head.</param>
    /// <exception cref="System.ArgumentException">The template is not a game object that can be shown.</exception>
    /// <exception cref="System.InvalidOperationException">The zone has unloaded.</exception>
    INpc Spawn(IZone zone, ulong templateId, Vector3 location, float orientation = 0, string? name = null);

    /// <summary>
    /// Removes every NPC this plugin spawned.
    /// </summary>
    void RemoveAll();

}
