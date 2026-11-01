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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Combat;
using Imlight.PluginApi;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Exposes a duel to plugins as an <see cref="IDuel"/>.
/// </summary>
/// <remarks>
/// The sub-circles belong to the duel's actor, so the participants are kept here as snapshots any actor can read. A
/// minion frees its circle from its own actor, which is why the lists are swapped atomically.
/// </remarks>
internal sealed class PluginDuel(Duel duel, IZone zone) : IDuel {

    private ImmutableList<IPlayer> _players = [];
    private ImmutableList<ICreature> _creatures = [];
    private volatile bool _hasEnded;

    public ulong Id => duel.m_duelID.Full;

    public IZone Zone => zone;

    public bool IsPvP => duel.m_bPVP;

    public int Round => duel.m_roundNum;

    public bool HasEnded => _hasEnded;

    public IReadOnlyList<IPlayer> Players => Volatile.Read(ref _players);

    public IReadOnlyList<ICreature> Creatures => [.. Volatile.Read(ref _creatures).Where(x => !x.IsDead)];

    public void AddParticipant(CombatDuelSubCircle circle) {
        if (circle.IsHumanPlayer) {
            var player = circle.ToPluginPlayer();
            _ = ImmutableInterlocked.Update(ref _players, x => x.Add(player));
        }
        else if (circle.ToPluginCreature() is { } creature) {
            _ = ImmutableInterlocked.Update(ref _creatures, x => x.Add(creature));
        }
    }

    /// <summary>
    /// Drops the player of a session. Creatures need no such call: they stop being listed once they are dead.
    /// </summary>
    public void RemovePlayer(IActorRef sessionActor)
        => ImmutableInterlocked.Update(
            ref _players, x => x.RemoveAll(player => ((PluginPlayer) player).SessionActor.Equals(sessionActor)));

    public void End() {
        _hasEnded = true;
        Volatile.Write(ref _players, []);
        Volatile.Write(ref _creatures, []);
    }

}
