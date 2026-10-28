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
 * PVP
 * ========================================================================
 * 
 * PURPOSE:
 * One match between two sides of queued players, from the moment the matchmaker grouped them. Plain state owned by the
 * PvPDirector; only the director thread uses it.
 * 
 * USAGE EXAMPLE:
 * var match = new PvPMatch(matchId, [[first], [second]]);
 * 
 * NOTE:
 * Side 0 is the first list of teams and side 1 the second; the side of a player is the index of their list. The queue entries carry the zone, display name and location each player queued from, which is where a
 * finished match sends them back. The instance owner id has the top bit set, which a character id can have too,
 * so only PvPRegistry says which owner ids belong to a match. The lobby session of a player is the one that was sent to the arena; a presence from another session in the
 * arena zone is the arrival.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Game.PvP.Matchmaking;

namespace Imlight.CoreLib.Game.PvP.Arena;

internal sealed class PvPMatch(ulong id, IReadOnlyList<IReadOnlyList<PvPQueueEntry>> teams) {

    public const ulong INSTANCE_OWNER_FLAG = 1UL << 63;

    private readonly HashSet<ulong> _accepted = [];
    private readonly HashSet<ulong> _arrived = [];

    public ulong Id { get; } = id;
    public IReadOnlyList<IReadOnlyList<PvPQueueEntry>> Teams { get; } = teams;
    public IReadOnlyList<PvPQueueEntry> Entries { get; } = [.. teams.SelectMany(team => team)];
    public IEnumerable<ulong> CharIds => Entries.Select(entry => entry.CharId);
    public PvPMatchState State { get; set; } = PvPMatchState.Confirming;
    public string ArenaZone { get; set; }
    public ulong InstanceOwnerId => INSTANCE_OWNER_FLAG | Id;
    public Dictionary<ulong, IActorRef> LobbySessions { get; } = [];
    public bool AllArrived => _arrived.Count == Entries.Count;
    public bool AllAccepted => _accepted.Count == Entries.Count;
    public bool IsRanked => Entries.All(entry => entry.Ranked is not null);

    public bool HasAccepted(ulong charId)
        => _accepted.Contains(charId);

    public bool Accept(ulong charId)
        => _accepted.Add(charId);

    public bool HasArrived(ulong charId)
        => _arrived.Contains(charId);

    public bool Arrive(ulong charId)
        => _arrived.Add(charId);

    public int GetSideIndex(ulong charId)
        => Teams.ToList().FindIndex(team => team.Any(entry => entry.CharId == charId));

    public PvPQueueEntry GetEntry(ulong charId)
        => Entries.FirstOrDefault(entry => entry.CharId == charId);

}
