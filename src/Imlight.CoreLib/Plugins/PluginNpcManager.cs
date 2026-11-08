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

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.PluginApi;
using Imlight.PluginApi.Npcs;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Spawns one plugin's NPCs, as an <see cref="INpcManager"/>.
/// </summary>
internal sealed class PluginNpcManager(ImlightPlugin owner) : INpcManager {

    public IReadOnlyList<INpc> Spawned
        => [.. PluginZone.All.SelectMany(x => x.Npcs).Where(x => ((PluginNpc) x).Owner == owner)];

    public INpc Spawn(IZone zone, ulong templateId, System.Numerics.Vector3 location, float orientation = 0,
                      string name = null) {
        ArgumentNullException.ThrowIfNull(zone);

        // Without a render behavior the entity has nothing to send the client.
        if (CoreObjectFactory.FindCoreTemplate(templateId) is not GameObjectTemplate template
            || !template.m_behaviors.Any(x => x is RenderBehaviorTemplate)) {
            throw new ArgumentException($"Template {templateId} is not a game object that can be shown.", nameof(templateId));
        }

        var pluginZone = (PluginZone) zone;
        var npc = new PluginNpc(owner, pluginZone, template, RandomGen.GenerateGUID().Full, location, orientation,
            string.IsNullOrWhiteSpace(name) ? null : name);
        if (!pluginZone.AddNpc(npc)) {
            throw new InvalidOperationException($"Zone {zone.Name} has unloaded.");
        }

        pluginZone.Send(new ZONE_102_PROTOCOL.MSG_SPAWNPLUGINNPC { Npc = npc });

        return npc;
    }

    public void RemoveAll() {
        foreach (var npc in Spawned) {
            npc.Remove();
        }
    }

}
