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
 * Builds the serialized MatchActor the client keeps for its own queue state. The client opens and closes its
 * status window from the status of this object.
 * 
 * USAGE EXAMPLE:
 * var actorBlob = PvPMatchActorBuilder.BuildQueued(entry, PvPMatchActorBuilder.StatusFindingMatch);
 * 
 * NOTE:
 * The idle actor equals the one a live server sends on login. The status values are the ones the client
 * reads: 4 finding a match, 8 match found, 10 waiting for the opponent, 11 waiting to teleport, 12 in the arena.
 * The time the player entered the queue is Unix seconds, which is inferred.
 * 
 * TODO:
 * What does the client do with m_enteredQueue? The idle actor of a live server carries 41.
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.Game.PvP.Matchmaking;
using Imlight.CoreLib.Game.PvP.Ranking;

namespace Imlight.CoreLib.Game.PvP.Arena;

internal static class PvPMatchActorBuilder {

    public const int STATUS_IDLE = 0;
    public const int STATUS_FINDING_MATCH = 4;
    public const int STATUS_MATCH_FOUND = 8;
    public const int STATUS_WAITING_FOR_OPPONENT = 10;
    public const int STATUS_WAITING_TO_TELEPORT = 11;
    public const int STATUS_IN_ARENA = 12;

    private const int IDLE_ENTERED_QUEUE = 41;

    /// <summary>
    /// The actor of a player who is not in any queue or match. Sending it resets the status window of the client.
    /// </summary>
    public static ByteString BuildIdle(ulong charId)
        => Serialize(Create(charId, STATUS_IDLE, null));

    public static ByteString BuildQueued(PvPQueueEntry entry, int status)
        => Serialize(entry.Ranked is null
            ? Create(entry.CharId, status, entry)
            : Fill(new AdvPvPActor(), entry.CharId, status, entry));

    /// <summary>
    /// The actor a hosted match lists for its host. The client lists a match only if some actor has a status other
    /// than zero, and draws a row only for a PvPActor, with its school, name blob and level.
    /// </summary>
    public static PvPActor BuildListed(PvPQueueEntry entry, PvPPlayerProfile profile, ulong matchId, ulong teamId) {
        var actor = Fill(new PvPActor(), entry.CharId, STATUS_FINDING_MATCH, entry);
        actor.m_nMatchID = matchId;
        actor.m_nTeamID = teamId;
        actor.m_level = (int) profile.Level;
        actor.m_nameBlob = new ByteString(profile.NameBlob);
        actor.m_sSchool = SchoolName(profile.SchoolId);

        return actor;
    }

    /// <summary>
    /// The placeholder the client draws as an open, joinable slot: an actor with the id zero.
    /// </summary>
    public static PvPActor BuildOpenSlot(PvPQueueEntry host, ulong matchId, ulong teamId) {
        var actor = Fill(new PvPActor(), 0, STATUS_IDLE, host);
        actor.m_nMatchID = matchId;
        actor.m_nTeamID = teamId;

        return actor;
    }

    private static string SchoolName(uint schoolId) => schoolId switch {
        1 => "Balance",
        2 => "Death",
        3 => "Fire",
        4 => "Ice",
        5 => "Life",
        6 => "Myth",
        7 => "Storm",
        _ => string.Empty
    };

    private static MatchActor Create(ulong charId, int status, PvPQueueEntry entry)
        => Fill(new MatchActor(), charId, status, entry);

    private static T Fill<T>(T actor, ulong charId, int status, PvPQueueEntry entry) where T : MatchActor {
        actor.m_nActorID = charId;
        actor.m_status = status;
        actor.m_costAdj = new MatchCostAdjustment { m_matchAdjustmentType = ADJUSTMENT_TYPE.UNSET_MAX };
        actor.m_overridingELO = -1;
        actor.m_matchKFactor = 32;
        actor.m_allowRangeToExceedMaxForMatches = true;
        actor.m_matchCrownsCost = -1;
        actor.m_enteredQueue = IDLE_ENTERED_QUEUE;
        actor.m_meetBracketBadgeRequirnments = true;
        actor.m_optInOut = "0";

        if (entry is not null) {
            actor.m_nTournamentNameID = entry.TournamentNameId;
            actor.m_nMatchNameID = entry.MatchNameId;
            actor.m_leagueID = entry.LeagueId;
            actor.m_seasonID = entry.SeasonId;
            actor.m_allowRangeToExceedMaxForMatches = entry.ExpandSearch;
            actor.m_enteredQueue = (int) new DateTimeOffset(entry.EnteredAtUtc).ToUnixTimeSeconds();
            actor.m_rating = entry.Rating;
            actor.m_1v1 = IsOneVersusOne(entry.MatchNameId);
            FillRanked(actor, entry.Ranked);
        }

        return actor;
    }

    private static bool IsOneVersusOne(uint matchNameId)
        => !PvPMatchFactory.TryGetMatch(matchNameId, out var match) || match.m_teamSize == 1;

    private static void FillRanked(MatchActor actor, PvPRatingSnapshot ranked) {
        if (ranked is null) {
            return;
        }

        actor.m_rating = ranked.Elo;
        actor.m_tier = ranked.RankIndex;
        actor.m_rank = ranked.RankIndex;
        actor.m_matchKFactor = ranked.KFactor;
        if (actor is AdvPvPActor advanced) {
            advanced.m_elo = ranked.Elo;
            advanced.m_wins = ranked.Wins;
            advanced.m_losses = ranked.Losses;
        }
    }

    private static ByteString Serialize(MatchActor actor) {
        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);
        if (!serializer.Serialize(actor, 4, out var data)) {
            Logger.Error("Failed to serialize the match actor of {CharId}.", Logger.Args((ulong) actor.m_nActorID));

            return default;
        }

        return data;
    }

}
