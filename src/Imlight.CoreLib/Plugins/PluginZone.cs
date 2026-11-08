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
 * PLUGIN ZONE
 * ========================================================================
 *
 * PURPOSE:
 * Exposes a zone actor to plugins as an IZone, with snapshots of its
 * players, creatures and duels that any actor can read.
 *
 * USAGE EXAMPLE:
 * var zone = PluginZone.Of(SessionActor.GetZoneActor());
 *
 * NOTE:
 * Only the owning zone actor calls Open, Close, AddPlayer and RemovePlayer.
 * Its entity actors call AddCreature, RemoveCreature, AddDuel and RemoveDuel,
 * so those swap the list atomically. Players, Creatures and Duels are replaced
 * on each change rather than mutated, so plugin handlers on other actors read
 * them without locking.
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/08/2026
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.PluginApi;
using Imlight.PluginApi.Npcs;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Exposes a zone actor to plugins as an <see cref="IZone"/>.
/// </summary>
internal sealed class PluginZone(string name, bool isInstance, IActorRef zoneActor) : IZone {

    private static readonly ConcurrentDictionary<IActorRef, PluginZone> s_zones = new();

    private volatile ImmutableList<IPlayer> _players = [];
    private ImmutableList<ICreature> _creatures = [];
    private ImmutableList<IDuel> _duels = [];
    // Guards _npcs with _closed, so an NPC cannot be added to a zone that has already let go of its NPCs.
    private readonly Lock _npcLock = new();
    private ImmutableList<INpc> _npcs = [];
    private bool _closed;

    public string Name => name;

    public bool IsInstance => isInstance;

    public IReadOnlyList<IPlayer> Players => _players;

    public IReadOnlyList<ICreature> Creatures => Volatile.Read(ref _creatures);

    public IReadOnlyList<IDuel> Duels => Volatile.Read(ref _duels);

    public IReadOnlyList<INpc> Npcs => Volatile.Read(ref _npcs);

    /// <summary>
    /// Every running zone. Safe to enumerate from any thread.
    /// </summary>
    public static IEnumerable<PluginZone> All => s_zones.Values;

    /// <summary>
    /// The zone for a zone actor, or null if the actor is null or not a running zone.
    /// </summary>
    public static PluginZone Of(IActorRef zoneActor)
        => zoneActor is not null && s_zones.TryGetValue(zoneActor, out var zone) ? zone : null;

    /// <summary>
    /// The plugin view of a session in this zone, the same instance <see cref="Players"/> holds when it has one.
    /// </summary>
    public IPlayer PlayerFor(Wizard wizard, IActorRef sessionActor)
        => _players.Find(x => ((PluginPlayer) x).SessionActor.Equals(sessionActor))
           ?? new PluginPlayer(wizard, sessionActor, this);

    public void Open() => s_zones[zoneActor] = this;

    public void Close() {
        _ = s_zones.TryRemove(new KeyValuePair<IActorRef, PluginZone>(zoneActor, this));
        _players = [];
        Volatile.Write(ref _creatures, []);

        // The entities stop with the zone, but one that never got to attach would not mark its NPC.
        ImmutableList<INpc> npcs;
        lock (_npcLock) {
            _closed = true;
            npcs = _npcs;
            Volatile.Write(ref _npcs, []);
        }

        foreach (var npc in npcs) {
            ((PluginNpc) npc).MarkStopped();
        }

        // A sigil has no stop hook, so a duel cut off by the zone unloading is ended here.
        foreach (var duel in Interlocked.Exchange(ref _duels, [])) {
            ((PluginDuel) duel).End();
        }
    }

    public void AddPlayer(Wizard wizard, IActorRef sessionActor)
        => _players = Without(sessionActor).Add(new PluginPlayer(wizard, sessionActor, this));

    public void RemovePlayer(IActorRef sessionActor)
        => _players = Without(sessionActor);

    /// <summary>
    /// The plugin view of a creature in this zone, or null if it is not listed, such as once its actor has stopped.
    /// </summary>
    public ICreature CreatureFor(IActorRef entityActor)
        => Volatile.Read(ref _creatures).Find(x => ((PluginCreature) x).Entity.SelfRef.Equals(entityActor));

    public PluginCreature AddCreature(ZoneEntity entity) {
        var creature = new PluginCreature(entity, this);
        _ = ImmutableInterlocked.Update(ref _creatures, x => x.Add(creature));

        return creature;
    }

    public void RemoveCreature(PluginCreature creature) {
        creature.MarkStopped();
        _ = ImmutableInterlocked.Update(ref _creatures, x => x.Remove(creature));
    }

    public void AddDuel(PluginDuel duel)
        => ImmutableInterlocked.Update(ref _duels, x => x.Add(duel));

    public void RemoveDuel(PluginDuel duel)
        => ImmutableInterlocked.Update(ref _duels, x => x.Remove(duel));

    /// <summary>
    /// Lists an NPC in this zone, and returns false if the zone has closed.
    /// </summary>
    public bool AddNpc(PluginNpc npc) {
        lock (_npcLock) {
            if (_closed) {
                return false;
            }

            Volatile.Write(ref _npcs, _npcs.Add(npc));

            return true;
        }
    }

    public void RemoveNpc(PluginNpc npc) {
        lock (_npcLock) {
            Volatile.Write(ref _npcs, _npcs.Remove(npc));
        }
    }

    /// <summary>
    /// Sends a message to the zone actor.
    /// </summary>
    public void Send(object message) => zoneActor.Tell(message, null);

    /// <summary>
    /// Sends a message through the zone's broadcast, as if the zone itself sent it.
    /// </summary>
    public void Broadcast(IMessage message, ZoneBroadcastTarget targets = ZoneBroadcastTarget.Players)
        => zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = message,
            Selfless = false,
            Targets = targets,
        });

    private ImmutableList<IPlayer> Without(IActorRef sessionActor)
        => _players.RemoveAll(x => ((PluginPlayer) x).SessionActor.Equals(sessionActor));

}
