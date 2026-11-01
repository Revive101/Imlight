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
 * One practice match a host created and listed, until every slot is taken or the host cancels it.
 * 
 * USAGE EXAMPLE:
 * var hosted = new PvPHostedMatch(id, hostEntry, profile, template, request);
 * hosted.TrySeat(joinerEntry, joinerProfile, teamId, out var teamIndex);
 * 
 * NOTE:
 * Every team lists one actor per slot: the seated players, then open slots (an actor with the id zero). The host
 * takes the first slot of team one. A joiner names a team by its id and gets the next free slot in it. Seating or
 * freeing a slot builds a new Info, so a reader of an earlier Info never sees it change.
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
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.PvP.Arena;

namespace Imlight.CoreLib.Game.PvP.Matchmaking;

internal sealed record PvPHostedSeat(PvPQueueEntry Entry, PvPPlayerProfile Profile);

internal sealed class PvPHostedMatch {

    // Status 4 means in progress to the client, which then shows the match on the watch tab only.
    private const int OPEN_STATUS = 0;

    // The client keys teams by id across all matches, so ids must not repeat between matches.
    private const ulong TEAM_ID_STRIDE = 10;

    private readonly PvPMatchRequest _request;
    private readonly string _matchName;
    private readonly uint _matchNameId;
    private readonly int _teamSize;
    private readonly List<List<PvPHostedSeat>> _seats;

    public ulong Id { get; }
    public PvPQueueEntry Host { get; }
    public PvPHostedState State { get; set; } = PvPHostedState.Open;
    public PvPMatchInfo Info { get; private set; }

    // The level a joiner may not exceed; zero means no limit.
    public int MaxActorLevel { get; }

    public bool IsFriendsOnly => Info.m_friendsOnly;
    public bool IsFull => _seats.All(team => team.Count == _teamSize);

    public IReadOnlyList<IReadOnlyList<PvPQueueEntry>> Teams
        => [.. _seats.Select(team => (IReadOnlyList<PvPQueueEntry>) [.. team.Select(seat => seat.Entry)])];

    public PvPHostedMatch(ulong id, PvPQueueEntry host, PvPPlayerProfile profile, MatchTemplate template, PvPMatchRequest request) {
        Id = id;
        Host = host;
        _request = request;
        _matchName = template.m_matchName;
        _matchNameId = StringHash.Compute(template.m_matchName);
        _teamSize = (int) template.m_teamSize;
        _seats = [.. Enumerable.Range(0, (int) template.m_teamCount).Select(_ => new List<PvPHostedSeat>())];
        _seats[0].Add(new PvPHostedSeat(host, profile));

        Info = BuildInfo();
        MaxActorLevel = Info.m_teams.Select(team => team.m_maxActorLevel).FirstOrDefault(level => level > 0);
    }

    public bool IsSeated(ulong charId)
        => _seats.Any(team => team.Any(seat => seat.Entry.CharId == charId));

    /// <summary>
    /// Seats a player in the team with this id, or, for team id zero, in the first team with a free slot.
    /// </summary>
    public bool TrySeat(PvPQueueEntry entry, PvPPlayerProfile profile, ulong teamId, out int teamIndex) {
        teamIndex = FindTeamWithFreeSlot(teamId);
        if (teamIndex < 0 || IsSeated(entry.CharId)) {
            return false;
        }

        _seats[teamIndex].Add(new PvPHostedSeat(entry, profile));
        Info = BuildInfo();

        return true;
    }

    public bool HasFreeSlot(ulong teamId)
        => FindTeamWithFreeSlot(teamId) >= 0;

    public bool Unseat(ulong charId) {
        var team = _seats.FirstOrDefault(seats => seats.Any(seat => seat.Entry.CharId == charId));
        if (team is null) {
            return false;
        }

        team.RemoveAll(seat => seat.Entry.CharId == charId);
        Info = BuildInfo();

        return true;
    }

    private int FindTeamWithFreeSlot(ulong teamId) {
        // todo: guess; a client that sends no team id is seated in the first team with a free slot.
        if (teamId == 0) {
            return _seats.FindIndex(team => team.Count < _teamSize);
        }

        var index = Info.m_teams.ToList().FindIndex(team => (ulong) team.m_nTeamID == teamId);

        return index >= 0 && _seats[index].Count < _teamSize ? index : -1;
    }

    private PvPMatchInfo BuildInfo() {
        var teams = new List<MatchTeam>();
        for (var i = 0; i < _seats.Count; i++) {
            teams.Add(BuildTeam(i));
        }

        return new PvPMatchInfo {
            m_matchID = Id,
            m_matchNameID = _matchNameId,
            m_matchName = _matchName,
            m_teams = teams,
            m_creatorID = Host.CharId,
            m_tournamentNameID = Host.TournamentNameId,
            m_leagueID = Host.LeagueId,
            m_seasonID = Host.SeasonId,
            m_friendsOnly = teams[0].m_friendsOnly,
            m_status = OPEN_STATUS,
            m_teamSize = (uint) _teamSize,
            m_joinQueueRequirements = _request.m_joinQueueRequirements,
            m_matchTimer = _request.m_matchTimer,
            m_bonusTime = _request.m_bonusTime,
            m_passPenalty = _request.m_passPenalty,
            m_yellowTime = _request.m_yellowTime,
            m_redTime = _request.m_redTime,
            m_minTurnTime = _request.m_minTurnTime,
            m_effects = _request.m_effects ?? []
        };
    }

    private MatchTeam BuildTeam(int index) {
        var requested = _request.m_teams is { Count: > 0 } ? _request.m_teams[index < _request.m_teams.Count ? index : 0] : null;
        var teamId = Id * TEAM_ID_STRIDE + (ulong) index + 1;
        var actors = _seats[index]
            .Select(seat => (MatchActor) PvPMatchActorBuilder.BuildListed(seat.Entry, seat.Profile, Id, teamId))
            .ToList();
        while (actors.Count < _teamSize) {
            actors.Add(PvPMatchActorBuilder.BuildOpenSlot(Host, Id, teamId));
        }

        return new MatchTeam {
            m_nTeamID = teamId,
            m_matchId = Id,
            m_matchNameID = _matchNameId,
            m_creatorId = Host.CharId,
            m_friendsOnly = requested?.m_friendsOnly ?? false,
            m_actors = actors,
            m_bracketID = requested?.m_bracketID ?? 0,
            m_leagueID = Host.LeagueId,
            m_seasonID = Host.SeasonId,
            m_maxActorLevel = requested?.m_maxActorLevel ?? 0
        };
    }

}
