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
 * What one session remembers of the PvP windows it opened: the kiosk it used and the tournament, league and
 * season of the PvP window. The client sends neither league nor season after it opens the window.
 * 
 * USAGE EXAMPLE:
 * var replies = _window.OpenWindow(message, SessionActor.ActorRef, playerObject, wizard, DateTime.UtcNow);
 * 
 * NOTE:
 * Each method answers with the wire messages to send. A request that did not come from a kiosk of this session
 * or names a league or season the player may not use is refused, never trusted.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP.Arena;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.Game.PvP.Matchmaking;
using Imlight.CoreLib.Game.PvP.Ranking;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.PvP;

internal sealed class PvPWindowState {

    private const byte RANKED_MATCH_TYPE = 1;
    private const byte PRACTICE_MATCH_TYPE = 2;
    private const sbyte CREATE_COMMAND = 1;
    private const sbyte JOIN_COMMAND = 3;
    private const sbyte LEAVE_COMMAND = 4;

    // todo: guess; the client reads 0 as success and anything else as a failure.
    private const byte HOST_FAILURE = 1;

    private uint _kioskLeagueId;

    public uint TournamentNameId { get; private set; }
    public uint LeagueId { get; private set; }
    public uint SeasonId { get; private set; }
    public bool IsRanked { get; private set; }

    public bool HasWindow => TournamentNameId != 0;

    public void KioskUsed(uint leagueId) {
        _kioskLeagueId = leagueId;
        TournamentNameId = 0;
        LeagueId = 0;
        SeasonId = 0;
        IsRanked = false;
    }

    public IReadOnlyList<IMessage> OpenWindow(WIZARD3_56_PROTOCOL.MSG_PVP5THAGEOPENPVPWINDOW message, IActorRef playerActor,
                                              CoreObject playerObject, Wizard wizard, DateTime utcNow) {
        if (!PvPSettings.IsEnabled) {
            return [new WIZARD3_56_PROTOCOL.MSG_PVPDISABLED()];
        }

        var ranked = message.MatchType == RANKED_MATCH_TYPE && PvPSettings.RankedEnabled;
        if (message.MatchType != PRACTICE_MATCH_TYPE && !ranked) {
            if (message.MatchType != RANKED_MATCH_TYPE) {
                Logger.Warning("{CharId} opened a PvP window of unknown match type {MatchType}.",
                    Logger.Args(wizard.CharId, message.MatchType));
            }

            return [PvPArenaErrors.Message(PvPArenaErrors.INVALID_TOURNAMENT_TYPE)];
        }

        var refusal = GetOpenRefusal(message.LeagueID, message.SeasonID, playerActor, playerObject, wizard, utcNow);
        if (refusal is not null) {
            return [PvPArenaErrors.Message(refusal)];
        }

        if (ranked && !IsRatedLeague(message.LeagueID)) {
            return [PvPArenaErrors.Message(PvPArenaErrors.PLAYER_INELIGIBLE)];
        }

        if (!TryGetOpenTournament(ranked, out var tournament)) {
            return [PvPArenaErrors.Message(PvPArenaErrors.INVALID_TOURNAMENT_TYPE)];
        }

        TournamentNameId = StringHash.Compute(tournament.m_tournamentName);
        LeagueId = message.LeagueID;
        SeasonId = message.SeasonID;
        IsRanked = ranked;

        return [new WIZARD_12_PROTOCOL.MSG_PREPVPKIOSK {
            TournamentNameID = TournamentNameId,
            LeagueID = LeagueId,
            SeasonID = SeasonId,
            Patching = 0
        }];
    }

    private static bool IsRatedLeague(uint leagueId)
        => PvPLeagueFactory.TryGetLeague(leagueId, out var league)
           && PvPLeagueFactory.TryGetRankTable(league.m_leagueType, out var table)
           && table.IsRated;

    private static bool TryGetOpenTournament(bool ranked, out TournamentTemplate tournament) {
        if (ranked) {
            if (PvPTournamentFactory.TryGetRankedTournament(out tournament)) {
                return true;
            }

            Logger.Error("No tournament of the data can be used for ranked play.");

            return false;
        }

        if (PvPTournamentFactory.TryGetPracticeTournament(out tournament)) {
            return true;
        }

        Logger.Error("No tournament of the data can be used for practice play.");

        return false;
    }

    public IReadOnlyList<IMessage> CanJoin(WIZARD3_56_PROTOCOL.MSG_PVP5THAGECANJOINMATCH message, IActorRef playerActor,
                                           CoreObject playerObject, Wizard wizard, DateTime utcNow) {
        var canJoin = HasWindow
                      && message.LeagueID == LeagueId
                      && message.SeasonID == SeasonId
                      && GetOpenRefusal(LeagueId, SeasonId, playerActor, playerObject, wizard, utcNow, requireKiosk: false) is null;

        return [new WIZARD3_56_PROTOCOL.MSG_PVP5THAGECANJOINMATCHRESPONSE { CanJoinQueue = (byte) (canJoin ? 1 : 0) }];
    }

    /// <summary>
    /// Answers the client echo of MSG_PREPVPKIOSK: it asks for the full tournament info once the window exists.
    /// </summary>
    public IReadOnlyList<IMessage> PrePvPKioskEcho(WIZARD_12_PROTOCOL.MSG_PREPVPKIOSK message, Wizard wizard) {
        // Patching means the client is still downloading arena zone packages; it echoes again when it is done.
        if (message.Patching != 0) {
            return [];
        }

        if (!HasWindow || message.TournamentNameID != TournamentNameId || message.LeagueID != LeagueId || message.SeasonID != SeasonId) {
            Logger.Warning("{CharId} echoed a PvP kiosk window that is not the one opened.", Logger.Args(wizard.CharId));

            return [];
        }

        return [GetUpdateInfo(wizard, "kiosk echo", message.LeagueID, message.SeasonID)];
    }

    public IReadOnlyList<IMessage> UpdateRequest(WIZARD_12_PROTOCOL.MSG_PVPUPDATEREQUEST message, Wizard wizard) {
        if (!TryReadRequest(message.InfoRequest, out var request)) {
            Logger.Warning("{CharId} sent a PvP update request that does not deserialize.", Logger.Args(wizard.CharId));

            return [];
        }

        if (!HasWindow || request.m_tournamentNameID != TournamentNameId) {
            Logger.Warning("{CharId} asked for the PvP tournament {TournamentId}, which is not the one opened.",
                Logger.Args(wizard.CharId, request.m_tournamentNameID));

            return [];
        }

        return [GetUpdateInfo(wizard, "update request", request.m_leagueID, request.m_seasonID)];
    }

    private static bool TryReadRequest(ByteString data, out TournamentInfoRequest request)
        => TryRead(data, out request);

    private static bool TryRead<T>(ByteString data, out T value) where T : PropertyClass {
        value = null;
        try {
            var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);

            return serializer.Deserialize(data, 4, out value) && value is not null;
        } catch (Exception) {
            return false;
        }
    }

    /// <summary>
    /// Answers the create, join and leave commands of the PvP window: the wire messages to send back and the message
    /// for the PvPDirector, if the command is in order.
    /// </summary>
    public (IReadOnlyList<IMessage> Replies, IServerMessage ToDirector) Intent(WIZARD_12_PROTOCOL.MSG_PVPINTENT message, IActorRef playerActor,
                                                                              CoreObject playerObject, Wizard wizard, DateTime utcNow) {
        switch (message.Command) {
            case CREATE_COMMAND:
                return Host(message, playerActor, playerObject, wizard, utcNow);
            case JOIN_COMMAND:
                return Join(message, playerActor, playerObject, wizard, utcNow);
            case LEAVE_COMMAND:
                return ([], new PVP_111_PROTOCOL.MSG_LEAVEQUEUE { CharId = wizard.CharId });
            default:
                Logger.Warning("{CharId} sent PvP intent command {Command}, which practice does not offer.",
                    Logger.Args(wizard.CharId, message.Command));

                return ([PvPArenaErrors.Message(PvPArenaErrors.INVALID_TOURNAMENT_TYPE)], null);
        }
    }

    /// <summary>
    /// Answers the Try All Opponents choice. The client also sends it unprompted right after it receives the
    /// first queued match actor.
    /// </summary>
    public PVP_111_PROTOCOL.MSG_SETEXPANDSEARCH ExpandSearch(WIZARD_12_PROTOCOL.MSG_EXPANDPVPSEARCH message, Wizard wizard) {
        if (!HasWindow || message.TournamentNameID != TournamentNameId || !PvPRegistry.IsQueued(wizard.CharId)) {
            return null;
        }

        return new PVP_111_PROTOCOL.MSG_SETEXPANDSEARCH { CharId = wizard.CharId, Expand = message.ExpandMatchSearch != 0 };
    }

    private (IReadOnlyList<IMessage> Replies, IServerMessage ToDirector) Join(WIZARD_12_PROTOCOL.MSG_PVPINTENT message, IActorRef playerActor,
                                                                             CoreObject playerObject, Wizard wizard, DateTime utcNow) {
        if (!PvPSettings.IsEnabled) {
            return ([new WIZARD3_56_PROTOCOL.MSG_PVPDISABLED()], null);
        }

        if (!HasWindow || message.TournamentNameID != TournamentNameId
            || !PvPTournamentFactory.TryGetTournament(TournamentNameId, out var tournament)) {
            Logger.Warning("{CharId} tried to join tournament {TournamentId} match {MatchId} without that window.",
                Logger.Args(wizard.CharId, message.TournamentNameID, message.MatchID));

            return ([], null);
        }

        if (message.MatchID != 0) {
            return JoinHosted(message, playerActor, playerObject, wizard, utcNow);
        }

        Logger.Debug("{CharId} queue join from the tab of match id {MatchNameId}.", Logger.Args(wizard.CharId, message.MatchNameID));

        var matchRefusal = GetMatchRefusal(tournament, message.MatchNameID);
        if (matchRefusal is not null) {
            return ([PvPArenaErrors.Message(matchRefusal)], null);
        }

        var refusal = GetOpenRefusal(LeagueId, SeasonId, playerActor, playerObject, wizard, utcNow, requireKiosk: false);
        if (refusal is not null) {
            return ([PvPArenaErrors.Message(refusal)], null);
        }

        PvPRatingSnapshot snapshot = null;
        if (IsRanked && !PvPRatingSnapshot.TryCreate(wizard, LeagueId, SeasonId, utcNow, out snapshot)) {
            return ([PvPArenaErrors.Message(PvPArenaErrors.INVALID_TOURNAMENT_TYPE)], null);
        }

        return ([], new PVP_111_PROTOCOL.MSG_JOINQUEUE {
            CharId = wizard.CharId,
            TournamentNameId = TournamentNameId,
            MatchNameId = message.MatchNameID,
            LeagueId = LeagueId,
            SeasonId = SeasonId,
            ExpandSearch = message.AllowExtMatchSearch != 0,
            Ranked = snapshot
        });
    }

    private (IReadOnlyList<IMessage> Replies, IServerMessage ToDirector) JoinHosted(WIZARD_12_PROTOCOL.MSG_PVPINTENT message, IActorRef playerActor,
                                                                                    CoreObject playerObject, Wizard wizard, DateTime utcNow) {
        if (IsRanked) {
            return ([PvPArenaErrors.Message(PvPArenaErrors.INVALID_TOURNAMENT_TYPE)], null);
        }

        // todo: guess; a hosted join is command 3 with the MatchID of the row.
        var refusal = GetOpenRefusal(LeagueId, SeasonId, playerActor, playerObject, wizard, utcNow, requireKiosk: false);
        if (refusal is not null) {
            return ([PvPArenaErrors.Message(refusal)], null);
        }

        return ([], new PVP_111_PROTOCOL.MSG_JOINHOSTED {
            CharId = wizard.CharId,
            MatchId = message.MatchID,
            TournamentNameId = TournamentNameId,
            LeagueId = LeagueId,
            SeasonId = SeasonId,
            TeamId = message.TargetObject
        });
    }

    private (IReadOnlyList<IMessage> Replies, IServerMessage ToDirector) Host(WIZARD_12_PROTOCOL.MSG_PVPINTENT message, IActorRef playerActor,
                                                                             CoreObject playerObject, Wizard wizard, DateTime utcNow) {
        if (!PvPSettings.IsEnabled) {
            return ([new WIZARD3_56_PROTOCOL.MSG_PVPDISABLED()], null);
        }

        if (!HasWindow || message.TournamentNameID != TournamentNameId
            || !PvPTournamentFactory.TryGetTournament(TournamentNameId, out var tournament)) {
            Logger.Warning("{CharId} tried to host in tournament {TournamentId} without that window.",
                Logger.Args(wizard.CharId, message.TournamentNameID));

            return ([], null);
        }

        if (IsRanked) {
            return ([HostReply(wizard, HOST_FAILURE), PvPArenaErrors.Message(PvPArenaErrors.INVALID_TOURNAMENT_TYPE)], null);
        }

        if (!TryRead<PvPMatchRequest>(message.TargetData, out var request) || !IsHostTeam(request, wizard.CharId)) {
            Logger.Warning("{CharId} sent a PvP match request that does not deserialize or has no host team.", Logger.Args(wizard.CharId));

            return ([HostReply(wizard, HOST_FAILURE)], null);
        }

        // The league and season of the window are trusted over the ones inside the request.
        var refusal = GetMatchRefusal(tournament, request.m_matchNameID)
                      ?? GetOpenRefusal(LeagueId, SeasonId, playerActor, playerObject, wizard, utcNow, requireKiosk: false);
        if (refusal is not null) {
            return ([HostReply(wizard, HOST_FAILURE), PvPArenaErrors.Message(refusal)], null);
        }

        // The director replies with the success.
        return ([], new PVP_111_PROTOCOL.MSG_HOSTMATCH {
            CharId = wizard.CharId,
            TournamentNameId = TournamentNameId,
            MatchNameId = request.m_matchNameID,
            LeagueId = LeagueId,
            SeasonId = SeasonId,
            Request = request
        });
    }

    private static bool IsHostTeam(PvPMatchRequest request, ulong charId)
        => request.m_teams is { Count: > 0 }
           && (request.m_teams[0].m_actors ?? []).All(actor => (ulong) actor.m_nActorID == charId);

    private WIZARD_12_PROTOCOL.MSG_PVPMATCHREQUEST HostReply(Wizard wizard, byte failure)
        => new() { TournamentNameID = TournamentNameId, MobileID = wizard.CharId, Failure = failure };

    private static string GetMatchRefusal(TournamentTemplate tournament, uint matchNameId) {
        if (!PvPMatchFactory.TryGetMatch(matchNameId, out var match)) {
            return PvPArenaErrors.INVALID_MATCH_NAME_ID;
        }

        string matchName = match.m_matchName;
        if (!tournament.m_allowedMatches.Any(name => (string) name == matchName)) {
            return PvPArenaErrors.MATCH_NOT_ALLOWED;
        }

        return PvPMatchFactory.IsSupportedLayout(match) ? null : PvPArenaErrors.INVALID_MATCH_NAME_ID;
    }

    private GAME_5_PROTOCOL.MSG_PVPUPDATEINFO GetUpdateInfo(Wizard wizard, string source, uint askedLeagueId, uint askedSeasonId) {
        var scope = new PvPListScope(TournamentNameId, LeagueId, SeasonId);
        var hosted = PvPRegistry.GetHostedMatches(scope);
        var update = PvPBlobs.UpdateInfo(wizard.CharId, PvPBlobs.TournamentList(scope));
        Logger.Debug("PvP list for {CharId} ({Source}, {Kind}): tournament {TournamentId}, asked league {AskedLeague} season {AskedSeason}, window league {League} season {Season}, "
                     + "IsPvPQueue {IsPvPQueue}, DiffType {DiffType}, {MatchCount} hosted match(es), {TeamCount} top-level team(s).",
            Logger.Args(wizard.CharId, source, scope.Kind, TournamentNameId, askedLeagueId, askedSeasonId, LeagueId, SeasonId,
                update.IsPvPQueue, update.DiffType, hosted.Count, hosted.Sum(info => info.m_teams.Count)));

        return update;
    }

    private string GetOpenRefusal(uint leagueId, uint seasonId, IActorRef playerActor, CoreObject playerObject, Wizard wizard,
                                  DateTime utcNow, bool requireKiosk = true) {
        if (requireKiosk && (_kioskLeagueId == 0 || leagueId != _kioskLeagueId)) {
            Logger.Warning("{CharId} opened a PvP window without using a kiosk of that league.", Logger.Args(wizard.CharId));

            return PvPArenaErrors.PLAYER_INELIGIBLE;
        }

        if (!PvPLeagueFactory.TryGetLeague(leagueId, out var league) || !PvPSeasonClock.IsLeagueOpen(league) || !PvPSeasonClock.IsOpen(league, seasonId, utcNow)) {
            return PvPArenaErrors.PLAYER_INELIGIBLE;
        }

        if (!PvPEligibility.MeetsLeagueRequirements(league, playerActor, playerObject, wizard)) {
            return PvPArenaErrors.NOT_IN_LEVEL_RANGE;
        }

        return PvPEligibility.GetQueueRefusal(wizard);
    }

}
