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
 * The PvP side of a duel: which match it is, who plays it, and whose half of the round it is.
 * 
 * USAGE EXAMPLE:
 * var state = new PvPDuelState(matchId, tournamentNameId, leagueId, roster, rules, CombatTeam.Player);
 * 
 * NOTE:
 * A round of an alternating duel is two half turns: the team that starts acts first, then the other team. A round
 * that does not alternate swaps its starting team every round.
 * 
 * TODO:
 * Is the swap of the starting team every round what live does? No client code reads the initiative switch fields.
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.CoreLib.Game.Combat;

namespace Imlight.CoreLib.Game.PvP.Arena;

internal sealed class PvPDuelState(
    ulong matchId,
    uint tournamentNameId,
    uint leagueId,
    IReadOnlyList<PvPRosterEntry> roster,
    PvPDuelRules rules,
    CombatTeam firstTeam) {

    private readonly HashSet<ulong> _forfeitedCharIds = [];
    private readonly HashSet<IActorRef> _disconnectedSessions = [];
    private readonly HashSet<IActorRef> _sessionsToWaitFor = [];

    public ulong MatchId { get; } = matchId;
    public uint TournamentNameId { get; } = tournamentNameId;
    public uint LeagueId { get; } = leagueId;
    public IReadOnlyList<PvPRosterEntry> Roster { get; } = roster;
    public PvPDuelRules Rules { get; } = rules;
    public CombatTeam OriginalFirstTeam { get; } = firstTeam;
    public CombatTeam RoundStarter { get; private set; } = firstTeam;
    public bool IsEnding { get; private set; }
    public bool IsFinished { get; private set; }
    public int WinningTeam { get; private set; } = -1;
    public bool HasSessionsToWaitFor => _sessionsToWaitFor.Count > 0;
    public IReadOnlyCollection<ulong> ForfeitedCharIds => _forfeitedCharIds;
    public int HalfTurn { get; private set; }
    public CombatTeam? ActingTeam
        => !Rules.AlternateTurns || HalfTurn == 0 ? null : HalfTurn == 2 ? Opposite(RoundStarter) : RoundStarter;

    public void BeginRound(int roundNumber) {
        // An alternating duel keeps its starter, otherwise a team would act last in a round and first in the next.
        if (roundNumber > 1 && !Rules.AlternateTurns) {
            RoundStarter = Opposite(RoundStarter);
        }

        HalfTurn = 1;
    }

    /// <summary>
    /// Moves a round that alternates on to the half turn of the other team.
    /// </summary>
    /// <returns><c>true</c> when the round has a second half turn to play.</returns>
    public bool TryBeginSecondHalfTurn() {
        if (!Rules.AlternateTurns || HalfTurn != 1) {
            return false;
        }

        HalfTurn = 2;

        return true;
    }

    /// <summary>
    /// Marks the duel as decided. The clients of <paramref name="sessions"/> are given time to finish the victory
    /// scene.
    /// </summary>
    /// <returns><c>false</c> when the duel was decided before.</returns>
    public bool BeginEnding(int winningTeam, IEnumerable<IActorRef> sessions) {
        if (IsEnding) {
            return false;
        }

        IsEnding = true;
        WinningTeam = winningTeam;
        _sessionsToWaitFor.UnionWith(sessions);

        return true;
    }

    /// <summary>
    /// Stops waiting for a session: its client finished the scene or is gone.
    /// </summary>
    /// <returns><c>true</c> when no session is left to wait for.</returns>
    public bool StopWaitingFor(IActorRef session) {
        _sessionsToWaitFor.Remove(session);

        return IsEnding && _sessionsToWaitFor.Count == 0;
    }

    /// <summary>
    /// Marks the duel as over for good.
    /// </summary>
    /// <returns><c>false</c> when it was over before.</returns>
    public bool TryFinish() {
        if (IsFinished) {
            return false;
        }

        IsFinished = true;

        return true;
    }

    public bool HasForfeited(ulong charId)
        => _forfeitedCharIds.Contains(charId);

    public void MarkForfeited(ulong charId)
        => _forfeitedCharIds.Add(charId);

    public bool IsDisconnected(IActorRef session)
        => _disconnectedSessions.Contains(session);

    public void MarkDisconnected(IActorRef session)
        => _disconnectedSessions.Add(session);

    public static CombatTeam Opposite(CombatTeam team)
        => team == CombatTeam.Player ? CombatTeam.Monster : CombatTeam.Player;

}
