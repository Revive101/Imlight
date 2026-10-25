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
 * Exposes a zone actor to plugins as an IZone, with a snapshot of its
 * players that any actor can read.
 *
 * USAGE EXAMPLE:
 * var zone = PluginZone.Of(SessionActor.GetZoneActor());
 *
 * NOTE:
 * Only the owning zone actor calls Open, Close, AddPlayer and RemovePlayer.
 * Players is replaced on each change rather than mutated, so plugin
 * handlers on other actors read it without locking.
 *
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Exposes a zone actor to plugins as an <see cref="IZone"/>.
/// </summary>
internal sealed class PluginZone(string name, bool isInstance, IActorRef zoneActor) : IZone {

    private static readonly ConcurrentDictionary<IActorRef, PluginZone> s_zones = new();

    private volatile ImmutableList<IPlayer> _players = [];

    public string Name => name;

    public bool IsInstance => isInstance;

    public IReadOnlyList<IPlayer> Players => _players;

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
    }

    public void AddPlayer(Wizard wizard, IActorRef sessionActor)
        => _players = Without(sessionActor).Add(new PluginPlayer(wizard, sessionActor, this));

    public void RemovePlayer(IActorRef sessionActor)
        => _players = Without(sessionActor);

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
