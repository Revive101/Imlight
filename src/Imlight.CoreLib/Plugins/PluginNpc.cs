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
using System.Threading;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;
using Imlight.PluginApi;
using Imlight.PluginApi.Npcs;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Exposes an NPC a plugin spawned as an <see cref="INpc"/>.
/// </summary>
/// <remarks>
/// It exists before its entity does, since a spawn reaches the zone later. The entity attaches itself once it loads,
/// so a call made before then is kept and the entity picks it up as it starts.
/// </remarks>
internal sealed class PluginNpc(
    ImlightPlugin owner,
    PluginZone zone,
    GameObjectTemplate template,
    ulong globalId,
    System.Numerics.Vector3 location,
    float orientation,
    string nameOverride) : INpc {

    private readonly Lock _lock = new();
    private readonly string _name = nameOverride ?? PluginTemplates.ResolveName(template);
    private IActorRef _entity;
    private bool _removed;
    private volatile Action<NpcInteraction> _interaction;
    private volatile string _state;
    private volatile NpcWizBang _wizBang;

    public ImlightPlugin Owner => owner;

    public GameObjectTemplate Template => template;

    /// <summary>
    /// The name the plugin gave the NPC, or null if it keeps its template's.
    /// </summary>
    public string NameOverride => nameOverride;

    /// <summary>
    /// The interaction handler, or null if the NPC is not interactable.
    /// </summary>
    public Action<NpcInteraction> Interaction => _interaction;

    /// <summary>
    /// The state the plugin last set, or null if it set none.
    /// </summary>
    public string State => _state;

    public ulong GlobalId => globalId;

    public ulong TemplateId => template.m_templateID;

    public string Name => _name;

    public IZone Zone => zone;

    public System.Numerics.Vector3 Location => location;

    public float Orientation => orientation;

    public bool IsRemoved {
        get {
            lock (_lock) {
                return _removed;
            }
        }
    }

    public bool IsInteractable => _interaction is not null;

    public void SetInteraction(Action<NpcInteraction> handler) => _interaction = handler;

    // The entity's InteractServiceMementoComponent reads this each second, so a change needs no message.
    public NpcWizBang WizBang => _wizBang;

    public void SetWizBang(NpcWizBang wizBang) {
        if (!Enum.IsDefined(wizBang)) {
            throw new ArgumentOutOfRangeException(nameof(wizBang), wizBang, "Not a wizbang.");
        }

        _wizBang = wizBang;
    }

    public void SetState(string state) {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        _state = state;
        Entity()?.Tell(new ZONE_102_PROTOCOL.MSG_SETPLUGINNPCSTATE { State = state }, null);
    }

    public void Remove() {
        IActorRef entity;
        lock (_lock) {
            if (_removed) {
                return;
            }

            _removed = true;
            entity = _entity;
        }

        zone.RemoveNpc(this);

        // An NPC whose entity has not attached yet removes itself as it attaches.
        entity?.Tell(new ZONE_102_PROTOCOL.MSG_REMOVEPLUGINNPC(), null);
    }

    /// <summary>
    /// Records the NPC's entity, and returns false if the NPC was removed before it attached.
    /// </summary>
    public bool Attach(IActorRef entity) {
        lock (_lock) {
            _entity = entity;

            return !_removed;
        }
    }

    /// <summary>
    /// Records that the NPC's entity has stopped, such as when its zone unloads.
    /// </summary>
    public void MarkStopped() {
        lock (_lock) {
            _removed = true;
            _entity = null;
        }

        zone.RemoveNpc(this);
    }

    private IActorRef Entity() {
        lock (_lock) {
            return _entity;
        }
    }

}
