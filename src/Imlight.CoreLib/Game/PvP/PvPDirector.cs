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
 * The single authority for PvP matchmaking: who is queued, who is in a match, and the client state each player
 * is shown. Plays the role the original matchmaking service played.
 * 
 * USAGE EXAMPLE:
 * Created once by the GameServerPool. Sessions talk to it through PvPService with PVP_111_PROTOCOL messages;
 * it answers each client with generated wire messages sent straight to the session.
 * 
 * NOTE:
 * One instance serves every realm and zone, so a queue entry survives zone transfers. Each transfer is a new
 * session; players re-announce themselves on arrival. Messages for a player between sessions are queued and
 * replayed on arrival. Other systems read PvPRegistry.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP.Arena;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.Game.PvP.Matchmaking;
using Imlight.CoreLib.Game.PvP.Ranking;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.PvP;

/// <summary>
/// Owns the PvP queue and every match on the server and speaks the PvP protocol to each player's client.
/// </summary>
internal sealed class PvPDirector : ReceiveProtocolDispatcher, IWithTimers {

    private const int MAX_QUEUED_MESSAGES_PER_PLAYER = 64;
    private const string MATCHMAKING_TIMER_KEY = "matchmaking";

    private static readonly TimeSpan s_matchmakingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_departureGrace = TimeSpan.FromSeconds(60);
    private static string DepartureTimerKey(ulong charId) => $"departure-{charId}";
    private static string ConfirmTimerKey(ulong matchId) => $"confirm-{matchId}";
    private static string ArrivalTimerKey(ulong matchId) => $"arrival-{matchId}";

    public static IActorRef Instance { get; private set; }

    public ITimerScheduler Timers { get; set; }

    private readonly Dictionary<ulong, IActorRef> _sessions = [];
    private readonly Dictionary<ulong, PvPPlayerProfile> _profiles = [];
    private readonly Dictionary<ulong, CoreObject> _playerObjects = [];
    private readonly Dictionary<ulong, List<object>> _queuedMessages = [];
    private readonly PvPQueue _queue = new();
    private readonly Dictionary<ulong, PvPMatch> _matches = [];
    private readonly Dictionary<ulong, ulong> _matchIdByPlayer = [];
    private readonly Dictionary<ulong, PvPStrandedPlayer> _strandedPlayers = [];
    private readonly Dictionary<ulong, PvPHostedMatch> _hostedMatches = [];
    private readonly Dictionary<ulong, ulong> _hostedIdByPlayer = [];
    private readonly Dictionary<ulong, PvPListScope> _windowViewers = [];
    private readonly HashSet<string> _failedArenas = new(StringComparer.OrdinalIgnoreCase);
    private ulong _matchSequence;
    private ulong _hostedSequence;

    public PvPDirector()
        => Instance = Self;

    public static Props Props()
        => Akka.Actor.Props.Create(() => new PvPDirector());

    protected override void PreStart() {
        Timers.StartPeriodicTimer(MATCHMAKING_TIMER_KEY, new PVP_111_PROTOCOL.MSG_MATCHMAKINGTICK(), s_matchmakingInterval);
        LogRankBadges();

        base.PreStart();
    }

    protected override bool AroundReceive(Receive receive, object message) {
        // A restart would rebuild the director empty and silently drop every queued player.
        try {
            return base.AroundReceive(receive, message);
        } catch (Exception ex) {
            Logger.Error("PvP could not handle {MessageType} and carries on without it. {Exception}",
                Logger.Args(message.GetType().Name, ex));

            return true;
        }
    }

    #region Presence

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_PLAYERPRESENT))]
    private void ReceivePlayerPresent(PVP_111_PROTOCOL.MSG_PLAYERPRESENT message) {
        var profile = message.Profile;
        var charId = profile.CharId;

        _sessions[charId] = message.Session;
        _profiles[charId] = profile;
        _playerObjects[charId] = message.PlayerObject;
        _windowViewers.Remove(charId);
        Timers.Cancel(DepartureTimerKey(charId));

        ReplayQueuedMessages(charId, message.Session);
        HandleArenaPresence(charId, profile, message.Session);
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_PLAYERDEPARTED))]
    private void ReceivePlayerDeparted(PVP_111_PROTOCOL.MSG_PLAYERDEPARTED message) {
        var charId = message.CharId;

        // A zone transfer can attach the next session before the previous one finishes disposing.
        if (!_sessions.TryGetValue(charId, out var session) || !session.Equals(message.Session)) {
            return;
        }

        _sessions.Remove(charId);

        // A stranded player zones on their way and announces themselves again; only a logout ends the wait.
        if (message.IsLogout) {
            _strandedPlayers.Remove(charId);
        }

        if (message.IsLogout || !IsQueuedOrInMatch(charId)) {
            Leave(charId);

            return;
        }

        Timers.StartSingleTimer(
            DepartureTimerKey(charId),
            new PVP_111_PROTOCOL.MSG_DEPARTUREGRACEEXPIRED { CharId = charId },
            s_departureGrace
        );
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_DEPARTUREGRACEEXPIRED))]
    private void ReceiveDepartureGraceExpired(PVP_111_PROTOCOL.MSG_DEPARTUREGRACEEXPIRED message) {
        var charId = message.CharId;
        if (_sessions.ContainsKey(charId)) {
            return;
        }

        Logger.Debug("Character {CharId} did not return in time and leaves PvP.", Logger.Args(charId));

        Leave(charId);
    }

    #endregion

    #region Queue

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_JOINQUEUE))]
    private void ReceiveJoinQueue(PVP_111_PROTOCOL.MSG_JOINQUEUE message) {
        var charId = message.CharId;
        if (!_sessions.ContainsKey(charId) || !_profiles.TryGetValue(charId, out var profile)) {
            return;
        }

        if (_queue.Contains(charId)) {
            SendToPlayer(charId, PvPArenaErrors.Message(PvPArenaErrors.ALREADY_QUEUED));

            return;
        }

        if (PvPRegistry.IsInMatch(charId)) {
            SendToPlayer(charId, PvPArenaErrors.Message(PvPArenaErrors.ALREADY_IN_MATCH));

            return;
        }

        if (PvPRegistry.IsHosting(charId)) {
            SendToPlayer(charId, PvPArenaErrors.Message(PvPArenaErrors.ALREADY_QUEUED));

            return;
        }

        if (IsRankedTournament(message.TournamentNameId) != (message.Ranked is not null)) {
            SendToPlayer(charId, PvPArenaErrors.Message(PvPArenaErrors.INVALID_TOURNAMENT_TYPE));

            return;
        }

        if (!TryBuildEntry(profile, message.TournamentNameId, message.MatchNameId, message.LeagueId, message.SeasonId, message.ExpandSearch, message.Ranked, out var entry)) {
            return;
        }

        _queue.Add(entry);
        PvPRegistry.SetQueued(charId, true);

        SendQueueStatus(entry);
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_SETEXPANDSEARCH))]
    private void ReceiveSetExpandSearch(PVP_111_PROTOCOL.MSG_SETEXPANDSEARCH message) {
        if (!_queue.TryGet(message.CharId, out var entry) || entry.ExpandSearch == message.Expand) {
            return;
        }

        entry = entry with { ExpandSearch = message.Expand };
        _queue.Update(entry);

        SendQueueStatus(entry);
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_MATCHMAKINGTICK))]
    private void ReceiveMatchmakingTick(PVP_111_PROTOCOL.MSG_MATCHMAKINGTICK message) {
        PurgeStrandedPlayers();

        if (_queue.Count < 2) {
            return;
        }

        foreach (var teams in PvPMatchmaker.FindMatches(_queue.Entries, DateTime.UtcNow, _sessions.ContainsKey)) {
            CreateMatch(teams);
        }
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_LEAVEQUEUE))]
    private void ReceiveLeaveQueue(PVP_111_PROTOCOL.MSG_LEAVEQUEUE message) {
        // todo: guess; a host cancels the open match with the same command a queued player leaves with.
        if (CancelHosted(message.CharId)) {
            return;
        }

        if (TryGetMatch(message.CharId, out var match)) {
            LeaveMatch(match, message.CharId);

            return;
        }

        RemoveFromQueue(message.CharId);
    }

    #endregion

    #region Hosted

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_WINDOWOPENED))]
    private void ReceiveWindowOpened(PVP_111_PROTOCOL.MSG_WINDOWOPENED message) {
        if (_sessions.ContainsKey(message.CharId)) {
            _windowViewers[message.CharId] = new PvPListScope(message.TournamentNameId, message.LeagueId, message.SeasonId);
        }
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_HOSTMATCH))]
    private void ReceiveHostMatch(PVP_111_PROTOCOL.MSG_HOSTMATCH message) {
        var charId = message.CharId;
        if (!_sessions.ContainsKey(charId) || !_profiles.TryGetValue(charId, out var profile)) {
            return;
        }

        if (IsQueuedOrInMatch(charId) || PvPRegistry.IsHosting(charId)) {
            SendToPlayer(charId, PvPArenaErrors.Message(PvPArenaErrors.ALREADY_QUEUED));
            SendToPlayer(charId, HostReply(message.TournamentNameId, charId, failure: 1));

            return;
        }

        if (!TryBuildEntry(profile, message.TournamentNameId, message.MatchNameId, message.LeagueId, message.SeasonId, false, null, out var entry)
            || !PvPMatchFactory.TryGetMatch(message.MatchNameId, out var template)) {
            SendToPlayer(charId, HostReply(message.TournamentNameId, charId, failure: 1));

            return;
        }

        var scope = new PvPListScope(message.TournamentNameId, message.LeagueId, message.SeasonId);
        var hosted = new PvPHostedMatch(++_hostedSequence, entry with { IsHosted = true }, profile, template, message.Request);
        Logger.Debug("PvP host request of {CharId}: m_matchNameID {RequestMatchNameId}, template {Template} ({TemplateId}), stored match id {StoredId}, teams {TeamCount}, team size {TeamSize}.",
            Logger.Args(charId, message.Request.m_matchNameID, (string) template.m_matchName, StringHash.Compute(template.m_matchName),
                hosted.Info.m_matchNameID, hosted.Info.m_teams.Count, template.m_teamSize));
        _hostedMatches[hosted.Id] = hosted;
        _hostedIdByPlayer[charId] = hosted.Id;
        PvPRegistry.SetHosting(charId, true);
        PvPRegistry.SetHostedMatch(hosted.Id, hosted.Info);

        Logger.Debug("{CharId} hosts PvP match {HostedId}.", Logger.Args(charId, hosted.Id));

        // todo: guess; Failure 0 is success and MobileID is the id of the new match.
        SendToPlayer(charId, HostReply(message.TournamentNameId, hosted.Id, failure: 0));
        SendToPlayer(charId, PvPBlobs.UpdateInfo(charId, PvPBlobs.TournamentList(scope)));
        PushToViewers(scope, exceptCharId: charId);
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_JOINHOSTED))]
    private void ReceiveJoinHosted(PVP_111_PROTOCOL.MSG_JOINHOSTED message) {
        var charId = message.CharId;
        if (!_sessions.ContainsKey(charId) || !_profiles.TryGetValue(charId, out var profile)) {
            return;
        }

        var refusal = GetJoinHostedRefusal(message, profile, out var hosted);
        if (refusal is not null) {
            SendToPlayer(charId, PvPArenaErrors.Message(refusal));

            return;
        }

        if (!TryBuildEntry(profile, hosted.Host.TournamentNameId, hosted.Host.MatchNameId, hosted.Host.LeagueId, hosted.Host.SeasonId, false, null, out var joiner)) {
            return;
        }

        if (!hosted.TrySeat(joiner with { IsHosted = true }, profile, message.TeamId, out var teamIndex)) {
            SendToPlayer(charId, PvPArenaErrors.Message(PvPArenaErrors.INVALID_MATCH_NAME_ID));

            return;
        }

        Logger.Debug("{CharId} takes a slot in team {TeamIndex} of hosted PvP match {HostedId}.", Logger.Args(charId, teamIndex, hosted.Id));

        _hostedIdByPlayer[charId] = hosted.Id;
        PvPRegistry.SetHosting(charId, true);
        PvPRegistry.SetHostedMatch(hosted.Id, hosted.Info);

        if (!hosted.IsFull) {
            PushHostedUpdate(hosted);

            return;
        }

        hosted.State = PvPHostedState.Full;
        var teams = hosted.Teams;
        RemoveHosted(hosted);
        CreateMatch(teams);
    }

    #endregion

    #region Matches

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_CONFIRMMATCH))]
    private void ReceiveConfirmMatch(PVP_111_PROTOCOL.MSG_CONFIRMMATCH message) {
        var charId = message.CharId;
        if (!TryGetMatch(charId, out var match) || match.State != PvPMatchState.Confirming) {
            Logger.Debug("{CharId} answered a PvP match prompt, but has no match to confirm.", Logger.Args(charId));

            return;
        }

        var entry = match.GetEntry(charId);
        if (entry.TournamentNameId != message.TournamentNameId) {
            Logger.Debug("{CharId} answered the prompt of tournament {TournamentId}, not the one of the match.",
                Logger.Args(charId, message.TournamentNameId));

            return;
        }

        if (!message.Accept) {
            CancelMatch(match, PvPCancelReason.Declined, charId);

            return;
        }

        if (!match.Accept(charId)) {
            return;
        }

        if (!match.AllAccepted) {
            SendStatus(entry, PvPMatchActorBuilder.STATUS_WAITING_FOR_OPPONENT);

            return;
        }

        BeginTravel(match);
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_MATCHCANCELLED))]
    private void ReceiveMatchCancelled(PVP_111_PROTOCOL.MSG_MATCHCANCELLED message) {
        if (!_matches.TryGetValue(message.MatchId, out var match)) {
            return;
        }

        if (message.Reason == PvPCancelReason.NoArena && match.ArenaZone is not null && _failedArenas.Add(match.ArenaZone)) {
            Logger.Error("Arena {ArenaZone} cannot host a PvP match and is not chosen again.", Logger.Args(match.ArenaZone));
        }

        CancelMatch(match, message.Reason, culpritCharId: 0);
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_MATCHENDED))]
    private void ReceiveMatchEnded(PVP_111_PROTOCOL.MSG_MATCHENDED message) {
        if (!_matches.TryGetValue(message.MatchId, out var match) || match.State != PvPMatchState.InProgress) {
            Logger.Debug("PvP match {MatchId} reported its end, but is not in progress.", Logger.Args(message.MatchId));

            return;
        }

        Logger.Debug("PvP match {MatchId} ended: winning team {WinningTeam}, {ForfeitCount} forfeit(s).",
            Logger.Args(match.Id, message.WinningTeam, message.ForfeitedCharIds.Count));

        ReportRatedResult(match, message.WinningTeam, message.ForfeitedCharIds);
        match.State = PvPMatchState.Ended;
        ForgetMatch(match);
        ReturnPlayers(match, culpritCharId: 0);
    }

    #endregion

    #region State

    private bool IsQueuedOrInMatch(ulong charId)
        => _queue.Contains(charId) || PvPRegistry.IsInMatch(charId);

    private void RemoveFromQueue(ulong charId) {
        if (_queue.Remove(charId)) {
            PvPRegistry.SetQueued(charId, false);
        }
    }

    private void CreateMatch(IReadOnlyList<IReadOnlyList<PvPQueueEntry>> teams) {
        var match = new PvPMatch(++_matchSequence, teams);
        _matches[match.Id] = match;

        foreach (var charId in match.CharIds) {
            _queue.Remove(charId);
            PvPRegistry.SetQueued(charId, false);
            PvPRegistry.SetInMatch(charId, true);
            _matchIdByPlayer[charId] = match.Id;
        }

        Logger.Debug("PvP match {MatchId} pairs {Sides}.", Logger.Args(match.Id,
            string.Join(" against ", teams.Select(team => string.Join(" and ", team.Select(entry => entry.CharId))))));

        PvPMatchFactory.TryGetMatch(match.Entries[0].MatchNameId, out var template);
        var confirmTimeout = TimeSpan.FromSeconds(Math.Max(1, template?.m_timeout ?? 0));
        Timers.StartSingleTimer(
            ConfirmTimerKey(match.Id),
            new PVP_111_PROTOCOL.MSG_MATCHCANCELLED { MatchId = match.Id, Reason = PvPCancelReason.ConfirmTimeout },
            confirmTimeout
        );

        foreach (var entry in match.Entries) {
            SendToPlayer(entry.CharId, new WIZARD_12_PROTOCOL.MSG_PVPCONFIRM {
                TournamentNameID = entry.TournamentNameId,
                Confirm = 0,
                MatchActor = PvPMatchActorBuilder.BuildQueued(entry, PvPMatchActorBuilder.STATUS_MATCH_FOUND)
            });
        }
    }

    private void BeginTravel(PvPMatch match) {
        PvPMatchFactory.TryGetMatch(match.Entries[0].MatchNameId, out var template);
        if (!TryChooseArena(match, template, out var arenaZone)) {
            Logger.Error("PvP match {MatchId} has no arena zone that exists.", Logger.Args(match.Id));
            CancelMatch(match, PvPCancelReason.NoArena, culpritCharId: 0);

            return;
        }

        Timers.Cancel(ConfirmTimerKey(match.Id));
        match.ArenaZone = arenaZone;
        match.State = PvPMatchState.Travelling;
        PvPRegistry.SetMatchInstanceOwner(match.InstanceOwnerId, true);

        Timers.StartSingleTimer(
            ArrivalTimerKey(match.Id),
            new PVP_111_PROTOCOL.MSG_MATCHCANCELLED { MatchId = match.Id, Reason = PvPCancelReason.ArrivalTimeout },
            GetArrivalTimeout(template)
        );

        foreach (var entry in match.Entries) {
            // The client attaches again after the transfer; the owner assigned here puts both players in one instance.
            ZoneInstanceOwners.Assign(entry.CharId, arenaZone, match.InstanceOwnerId);
            if (_sessions.TryGetValue(entry.CharId, out var lobbySession)) {
                match.LobbySessions[entry.CharId] = lobbySession;
            }

            SendStatus(entry, PvPMatchActorBuilder.STATUS_WAITING_TO_TELEPORT);
            SendToPlayer(entry.CharId, new PVP_111_PROTOCOL.MSG_ENTERMATCH {
                MatchId = match.Id,
                ArenaZone = arenaZone,
                InstanceOwnerId = match.InstanceOwnerId
            });
        }
    }

    private bool TryChooseArena(PvPMatch match, MatchTemplate template, out string arenaZone) {
        arenaZone = null;
        if (template is null) {
            return false;
        }

        // A zone a player already stands in would only teleport them within it, never into the private arena. They
        // may have left the zone they queued in.
        var occupiedZones = match.Entries
            .SelectMany(entry => _profiles.TryGetValue(entry.CharId, out var profile)
                ? new[] { entry.OriginZone, profile.ZonePath }
                : new[] { entry.OriginZone })
            .ToList();
        var arenas = template.m_zones
            .Select(zone => (string) zone)
            .Where(zone => AccessPassManager.DoesZoneExist(zone)
                           && !_failedArenas.Contains(zone)
                           && !occupiedZones.Contains(zone, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (arenas.Count == 0) {
            return false;
        }

        arenaZone = arenas[Random.Shared.Next(arenas.Count)];

        return true;
    }

    private static TimeSpan GetArrivalTimeout(MatchTemplate template)
        => TimeSpan.FromSeconds(Math.Max(1d, template?.m_incompleteMatchTimeout ?? 0));

    private void HandleArenaPresence(ulong charId, PvPPlayerProfile profile, IActorRef session) {
        if (TryGetMatch(charId, out var match) && match.State == PvPMatchState.Travelling
            && string.Equals(profile.ZonePath, match.ArenaZone, StringComparison.OrdinalIgnoreCase)
            && !(match.LobbySessions.TryGetValue(charId, out var lobbySession) && lobbySession.Equals(session))
            && match.Arrive(charId)) {
            var entry = match.GetEntry(charId);
            var returnTo = PvPArenaReturn.For(entry);
            SendToPlayer(charId, new PVP_111_PROTOCOL.MSG_MATCHJOINED {
                MatchId = match.Id,
                ReturnZone = returnTo.Zone,
                ReturnZoneDisplayName = returnTo.ZoneDisplayName,
                ReturnLocation = returnTo.Location
            });
            SendStatus(entry, PvPMatchActorBuilder.STATUS_IN_ARENA);

            if (match.AllArrived) {
                StartMatch(match);
            }

            return;
        }

        // A player who was on the way when the match was cancelled arrives into an arena without a match.
        if (_strandedPlayers.TryGetValue(charId, out var stranded)
            && stranded.ExpiresAtUtc > DateTime.UtcNow
            && string.Equals(profile.ZonePath, stranded.ArenaZone, StringComparison.OrdinalIgnoreCase)
            && !stranded.LobbySession.Equals(session)) {
            _strandedPlayers.Remove(charId);

            SendIdle(charId);
            SendToPlayer(charId, new PVP_111_PROTOCOL.MSG_LEAVEARENA {
                ReturnZone = stranded.ReturnTo.Zone,
                ReturnLocation = stranded.ReturnTo.Location
            });
        }
    }

    private void StartMatch(PvPMatch match) {
        Timers.Cancel(ArrivalTimerKey(match.Id));

        if (match.CharIds.Any(charId => !_sessions.ContainsKey(charId) || !_profiles.ContainsKey(charId) || !_playerObjects.ContainsKey(charId))) {
            CancelMatch(match, PvPCancelReason.OpponentLeft, culpritCharId: 0);

            return;
        }

        var zoneActors = match.CharIds.Select(charId => _profiles[charId].ZoneActor).Distinct().ToList();
        if (zoneActors.Count != 1 || zoneActors[0] is null || zoneActors[0].Equals(ActorRefs.Nobody)) {
            Logger.Error("The players of PvP match {MatchId} are not in one zone actor; the match is cancelled.", Logger.Args(match.Id));
            CancelMatch(match, PvPCancelReason.NoArena, culpritCharId: 0);

            return;
        }

        match.State = PvPMatchState.InProgress;

        var roster = match.Entries
            .Select(entry => new PvPRosterEntry(entry.CharId, _sessions[entry.CharId], _playerObjects[entry.CharId], match.GetSideIndex(entry.CharId)))
            .ToList();
        zoneActors[0].Tell(new ZONE_102_PROTOCOL.MSG_STARTPVPMATCH {
            MatchId = match.Id,
            TournamentNameId = match.Entries[0].TournamentNameId,
            LeagueId = match.Entries[0].LeagueId,
            Roster = roster
        });

        Logger.Debug("PvP match {MatchId} starts in {ArenaZone}.", Logger.Args(match.Id, match.ArenaZone));
    }

    private void PurgeStrandedPlayers() {
        if (_strandedPlayers.Count == 0) {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var charId in _strandedPlayers.Where(x => x.Value.ExpiresAtUtc <= now).Select(x => x.Key).ToList()) {
            _strandedPlayers.Remove(charId);
        }
    }

    private void CancelMatch(PvPMatch match, PvPCancelReason reason, ulong culpritCharId) {
        if (match.State == PvPMatchState.Ended) {
            return;
        }

        var wasInArenaPhase = match.State is PvPMatchState.Travelling or PvPMatchState.InProgress;
        match.State = PvPMatchState.Ended;
        ForgetMatch(match);

        Logger.Debug("PvP match {MatchId} is cancelled: {Reason}.", Logger.Args(match.Id, reason));

        if (wasInArenaPhase) {
            ReturnPlayers(match, culpritCharId);

            return;
        }

        foreach (var entry in match.Entries) {
            var isCulprit = entry.CharId == culpritCharId;
            var requeue = reason switch {
                PvPCancelReason.Declined or PvPCancelReason.OpponentLeft => !isCulprit,
                PvPCancelReason.ConfirmTimeout => match.HasAccepted(entry.CharId),
                _ => false
            };

            if (requeue && !entry.IsHosted) {
                if (reason == PvPCancelReason.Declined) {
                    SendToPlayer(entry.CharId, PvPArenaErrors.Message(PvPArenaErrors.PLAYER_DECLINED));
                }

                Requeue(entry);
            } else if (!isCulprit) {
                // The client of a player who declined resets itself, and one who left has no client to tell.
                SendIdle(entry.CharId);
            }
        }
    }

    private void ForgetMatch(PvPMatch match) {
        Timers.Cancel(ConfirmTimerKey(match.Id));
        Timers.Cancel(ArrivalTimerKey(match.Id));
        _matches.Remove(match.Id);
        PvPRegistry.SetMatchInstanceOwner(match.InstanceOwnerId, false);

        foreach (var entry in match.Entries) {
            _matchIdByPlayer.Remove(entry.CharId);
            PvPRegistry.SetInMatch(entry.CharId, false);
            if (match.ArenaZone is not null) {
                ZoneInstanceOwners.Clear(entry.CharId, match.ArenaZone);
            }
        }
    }

    private void ReturnPlayers(PvPMatch match, ulong culpritCharId) {
        // Every player gets an idle match status and the ones in the arena are sent back. A player still on the way
        // is sent back when they arrive.
        PvPMatchFactory.TryGetMatch(match.Entries[0].MatchNameId, out var template);

        foreach (var entry in match.Entries.Where(entry => entry.CharId != culpritCharId)) {
            var returnTo = PvPArenaReturn.For(entry);
            if (match.ArenaZone is not null && !match.HasArrived(entry.CharId)) {
                match.LobbySessions.TryGetValue(entry.CharId, out var lobbySession);
                _strandedPlayers[entry.CharId] = new PvPStrandedPlayer(
                    match.ArenaZone, lobbySession ?? ActorRefs.Nobody, returnTo, DateTime.UtcNow + GetArrivalTimeout(template));
                SendIdle(entry.CharId);

                continue;
            }

            SendIdle(entry.CharId);
            if (match.HasArrived(entry.CharId)) {
                SendToPlayer(entry.CharId, new PVP_111_PROTOCOL.MSG_LEAVEARENA {
                    ReturnZone = returnTo.Zone,
                    ReturnLocation = returnTo.Location
                });
            }
        }
    }

    private void Requeue(PvPQueueEntry entry) {
        if (!_profiles.ContainsKey(entry.CharId) || !_queue.Add(entry)) {
            return;
        }

        PvPRegistry.SetQueued(entry.CharId, true);
        SendQueueStatus(entry);
    }

    private bool TryGetMatch(ulong charId, out PvPMatch match) {
        match = null;

        return _matchIdByPlayer.TryGetValue(charId, out var matchId) && _matches.TryGetValue(matchId, out match);
    }

    private void SendQueueStatus(PvPQueueEntry entry)
        => SendStatus(entry, PvPMatchActorBuilder.STATUS_FINDING_MATCH);

    private void SendStatus(PvPQueueEntry entry, int status)
        => SendToPlayer(entry.CharId, new GAME_5_PROTOCOL.MSG_MATCHMAKERUPDATE {
            CharacterID = entry.CharId,
            MatchActor = PvPMatchActorBuilder.BuildQueued(entry, status)
        });

    private void SendIdle(ulong charId)
        => SendToPlayer(charId, new GAME_5_PROTOCOL.MSG_MATCHMAKERUPDATE {
            CharacterID = charId,
            MatchActor = PvPMatchActorBuilder.BuildIdle(charId)
        });

    private void Leave(ulong charId) {
        RemoveFromQueue(charId);
        CancelHosted(charId);
        _windowViewers.Remove(charId);

        // A match in progress is the business of its duel: the player forfeits there and the duel ends the match.
        if (TryGetMatch(charId, out var match) && match.State != PvPMatchState.InProgress) {
            CancelMatch(match, PvPCancelReason.OpponentLeft, charId);
        }

        Forget(charId);
    }

    private void Forget(ulong charId) {
        _profiles.Remove(charId);
        _playerObjects.Remove(charId);
        _queuedMessages.Remove(charId);
        Timers.Cancel(DepartureTimerKey(charId));
    }

    private void SendToPlayer(ulong charId, object message) {
        if (_sessions.TryGetValue(charId, out var session)) {
            session.Tell(message);

            return;
        }

        // Only players between sessions are queued; anyone else is simply offline.
        if (!_profiles.ContainsKey(charId)) {
            return;
        }

        if (!_queuedMessages.TryGetValue(charId, out var queue)) {
            queue = [];
            _queuedMessages[charId] = queue;
        }

        if (queue.Count < MAX_QUEUED_MESSAGES_PER_PLAYER) {
            queue.Add(message);
        }
    }

    private void ReplayQueuedMessages(ulong charId, IActorRef session) {
        if (!_queuedMessages.Remove(charId, out var queue)) {
            return;
        }

        foreach (var message in queue) {
            session.Tell(message);
        }
    }

    private static void LogRankBadges() {
        try {
            PvPRankBadges.LogResolution();
        } catch (Exception ex) {
            Logger.Warning("The PvP rank badges could not be checked ({Reason}); ranks still work, but their badges may not be granted.", Logger.Args(ex.Message));
        }
    }

    private static bool IsRankedTournament(uint tournamentNameId)
        => PvPTournamentFactory.TryGetTournament(tournamentNameId, out var tournament) && PvPTournamentFactory.IsRanked(tournament);

    private bool TryBuildEntry(PvPPlayerProfile profile, uint tournamentNameId, uint matchNameId, uint leagueId, uint seasonId,
                               bool expandSearch, PvPRatingSnapshot ranked, out PvPQueueEntry entry) {
        entry = null;
        if (!PvPMatchFactory.TryGetMatch(matchNameId, out var match)
            || !PvPMatchFactory.TryGetLevelModifiers(match.m_levelModifiersListID.Full, out var levelModifiers)) {
            Logger.Error("Match {MatchId} has no template or level table; {CharId} cannot play it.", Logger.Args(matchNameId, profile.CharId));
            SendToPlayer(profile.CharId, PvPArenaErrors.Message(PvPArenaErrors.INVALID_MATCH_NAME_ID));

            return false;
        }

        entry = new PvPQueueEntry(
            CharId: profile.CharId,
            RealmName: profile.RealmName,
            TournamentNameId: tournamentNameId,
            MatchNameId: matchNameId,
            LeagueId: leagueId,
            SeasonId: seasonId,
            ExpandSearch: expandSearch,
            EnteredAtUtc: DateTime.UtcNow,
            Rating: ranked?.Elo ?? (int) Math.Round(PvPMatchmaker.GetRating(levelModifiers, (int) profile.Level)),
            OriginZone: profile.ZonePath,
            OriginZoneDisplayName: profile.ZoneDisplayName,
            OriginLocation: profile.Location,
            Ranked: ranked
        );

        return true;
    }

    private void LeaveMatch(PvPMatch match, ulong charId) {
        if (match.State == PvPMatchState.InProgress) {
            ForfeitParticipant(match, charId);

            return;
        }

        CancelMatch(match, PvPCancelReason.OpponentLeft, charId);
    }

    private void ForfeitParticipant(PvPMatch match, ulong charId) {
        if (!_profiles.TryGetValue(charId, out var profile) || profile.ZoneActor is null) {
            return;
        }

        profile.ZoneActor.Tell(new ZONE_102_PROTOCOL.MSG_FORFEITPVPPARTICIPANT { MatchId = match.Id, CharId = charId });
    }

    private string GetJoinHostedRefusal(PVP_111_PROTOCOL.MSG_JOINHOSTED message, PvPPlayerProfile profile, out PvPHostedMatch hosted) {
        if (!_hostedMatches.TryGetValue(message.MatchId, out hosted) || hosted.State != PvPHostedState.Open
            || !HostScope(hosted).SameList(new PvPListScope(message.TournamentNameId, message.LeagueId, message.SeasonId))
            || !_sessions.ContainsKey(hosted.Host.CharId)) {
            return PvPArenaErrors.INVALID_MATCH_NAME_ID;
        }

        if (hosted.IsSeated(profile.CharId)) {
            return PvPArenaErrors.ALREADY_IN_MATCH;
        }

        if (IsQueuedOrInMatch(profile.CharId) || PvPRegistry.IsHosting(profile.CharId)) {
            return PvPArenaErrors.ALREADY_QUEUED;
        }

        if (!hosted.HasFreeSlot(message.TeamId)) {
            return PvPArenaErrors.INVALID_MATCH_NAME_ID;
        }

        if (hosted.MaxActorLevel > 0 && profile.Level > hosted.MaxActorLevel) {
            return PvPArenaErrors.NOT_IN_LEVEL_RANGE;
        }

        // todo: the buddy check of a friends-only match; no buddy lookup helper exists yet, so every joiner passes.
        return null;
    }

    private bool CancelHosted(ulong charId) {
        if (!_hostedIdByPlayer.TryGetValue(charId, out var hostedId) || !_hostedMatches.TryGetValue(hostedId, out var hosted)) {
            return false;
        }

        if (hosted.Host.CharId != charId) {
            Logger.Debug("{CharId} leaves hosted PvP match {HostedId}.", Logger.Args(charId, hostedId));

            Unseat(hosted, charId);

            return true;
        }

        Logger.Debug("{CharId} cancels hosted PvP match {HostedId}.", Logger.Args(charId, hostedId));

        RemoveHosted(hosted);

        return true;
    }

    private void Unseat(PvPHostedMatch hosted, ulong charId) {
        hosted.Unseat(charId);
        _hostedIdByPlayer.Remove(charId);
        PvPRegistry.SetHosting(charId, false);
        PvPRegistry.SetHostedMatch(hosted.Id, hosted.Info);

        PushHostedUpdate(hosted);
    }

    private void RemoveHosted(PvPHostedMatch hosted) {
        var seated = hosted.Teams.SelectMany(team => team).Select(entry => entry.CharId).ToList();
        _hostedMatches.Remove(hosted.Id);
        PvPRegistry.SetHostedMatch(hosted.Id, null);

        foreach (var charId in seated) {
            _hostedIdByPlayer.Remove(charId);
            PvPRegistry.SetHosting(charId, false);
        }

        PushToViewers(HostScope(hosted), members: seated);
    }

    private void PushHostedUpdate(PvPHostedMatch hosted)
        => PushToViewers(HostScope(hosted), members: hosted.Teams.SelectMany(team => team).Select(entry => entry.CharId));

    private static PvPListScope HostScope(PvPHostedMatch hosted)
        => new(hosted.Host.TournamentNameId, hosted.Host.LeagueId, hosted.Host.SeasonId);

    private void PushToViewers(PvPListScope scope, ulong exceptCharId = 0, IEnumerable<ulong> members = null) {
        // The client only accepts a whole new list, so every change resends it.
        var recipients = _windowViewers
            .Where(viewer => viewer.Value.SameList(scope))
            .Select(viewer => viewer.Key)
            .Concat(members ?? [])
            .Where(charId => charId != exceptCharId)
            .Distinct();

        foreach (var charId in recipients) {
            if (!_sessions.TryGetValue(charId, out var session)) {
                continue;
            }

            var viewerScope = _windowViewers.TryGetValue(charId, out var viewing) ? viewing : scope;
            var blob = PvPBlobs.TournamentList(viewerScope);
            Logger.Debug("PvP list push to {CharId}: tournament {TournamentId}, {Bytes} bytes.", Logger.Args(charId, viewerScope.TournamentNameId, blob.Length));
            session.Tell(PvPBlobs.UpdateInfo(charId, blob));
        }
    }

    private static WIZARD_12_PROTOCOL.MSG_PVPMATCHREQUEST HostReply(uint tournamentNameId, ulong mobileId, byte failure)
        => new() { TournamentNameID = tournamentNameId, MobileID = mobileId, Failure = failure };

    private void ReportRatedResult(PvPMatch match, int winningTeam, IReadOnlyCollection<ulong> forfeitedCharIds) {
        if (!match.IsRanked) {
            return;
        }

        foreach (var entry in match.Entries) {
            var side = match.GetSideIndex(entry.CharId);
            var opponents = match.Entries.Where(other => match.GetSideIndex(other.CharId) != side).ToList();
            var forfeited = forfeitedCharIds.Contains(entry.CharId);
            var opponentsForfeited = opponents.All(opponent => forfeitedCharIds.Contains(opponent.CharId));

            SendRatedResult(entry.CharId, new PVP_111_PROTOCOL.MSG_RATEDRESULT {
                MatchId = match.Id,
                LeagueId = entry.LeagueId,
                SeasonId = entry.SeasonId,
                OpponentElo = PvPEloCalculator.GetTeamAverage(opponents.Select(opponent => opponent.Ranked.Elo)),
                Score = GetRatedScore(side, winningTeam, forfeited, opponentsForfeited)
            });
        }
    }

    private static double GetRatedScore(int side, int winningTeam, bool forfeited, bool opponentsForfeited) {
        if (forfeited) {
            return PvPEloCalculator.LOSS;
        }

        if (opponentsForfeited || winningTeam == side) {
            return PvPEloCalculator.WIN;
        }

        return winningTeam < 0 ? PvPEloCalculator.DRAW : PvPEloCalculator.LOSS;
    }

    private void SendRatedResult(ulong charId, PVP_111_PROTOCOL.MSG_RATEDRESULT result) {
        if (_sessions.ContainsKey(charId) || _profiles.ContainsKey(charId)) {
            SendToPlayer(charId, result);

            return;
        }

        PvPRatedResults.ApplyOffline(charId, result);
    }

    #endregion

}
