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
 * Sends a character's PvP ratings to the client, applies the result of a ranked match to them and runs the rating commands.
 * 
 * USAGE EXAMPLE:
 * Attached to every game session by GameServiceFactory; the client asks for its records with MSG_AdvPvPEloData and the PvPDirector sends MSG_RATEDRESULT when a ranked match is decided.
 * 
 * NOTE:
 * A rank badge is awarded through the badge service, the same way the badge grant command does it. Ratings are saved with the character on every change.
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
using System.Globalization;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.Game.PvP.Matchmaking;
using Imlight.CoreLib.Game.PvP.Ranking;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class PvPRatingService(SessionActor sessionActor) : MessageService(sessionActor) {

    private sealed record LeagueContext(PvPLeagueTemplate League, PvPRankTable Table, PvPSeasonEntry Season) {

        public uint LeagueId => StringHash.Compute(League.m_leagueType);
        public uint SeasonId => StringHash.Compute(Season.Template.m_seasonName);

    }

    private ulong _eloRequestGlobalId;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PvPRatingService(parentActor));

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_AdvPvPEloData))]
    private void ReceiveAdvPvPEloData(WIZARD3_56_PROTOCOL.MSG_AdvPvPEloData message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        _eloRequestGlobalId = message.GlobalID;
        if (PvPSettings.RankedEnabled) {
            CreateMissingRecords(wizard);
        }

        SendEloData(wizard);
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_RATEDRESULT))]
    private void ReceiveRatedResult(PVP_111_PROTOCOL.MSG_RATEDRESULT message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        var change = PvPRatedResults.Apply(wizard.PvPEloBehavior, message.LeagueId, message.SeasonId, message.OpponentElo, message.Score, out var table);
        if (change is null) {
            Logger.Warning("The rated result of match {MatchId} names a league or season that is gone; {CharId} keeps their rating.",
                Logger.Args(message.MatchId, wizard.CharId));

            return;
        }

        Logger.Debug("Match {MatchId}: {CharId} rating {OldElo} to {NewElo}, rank {OldRank} to {NewRank}.",
            Logger.Args(message.MatchId, wizard.CharId, change.OldElo, change.NewElo, change.OldRankIndex, change.NewRankIndex));

        Finish(wizard, table, change);
    }

    [MessageHandler(typeof(PVP_111_PROTOCOL.MSG_RATINGCOMMAND))]
    private void ReceiveRatingCommand(PVP_111_PROTOCOL.MSG_RATINGCOMMAND message) {
        var wizard = GetActiveWizard();
        if (wizard is null) {
            return;
        }

        if (!TryResolveCommandLeague(wizard, message.League, out var context)) {
            Reply(string.IsNullOrEmpty(message.League)
                ? "No rated league with a running season is open to you."
                : $"'{message.League}' is not a rated league with a running season.");

            return;
        }

        switch (message.Action) {
            case "show":
                CommandShow(wizard, context);
                break;
            case "set":
                CommandSetRating(wizard, context, message.Value);
                break;
            case "rankset":
                CommandSetRank(wizard, context, message.Value);
                break;
            case "seasonreset":
                CommandSeasonReset(wizard, context);
                break;
            case "clear":
                CommandClear(wizard, context);
                break;
            default:
                Reply($"Unknown rating action '{message.Action}'.");
                break;
        }
    }

    private void CommandShow(Wizard wizard, LeagueContext context) {
        var record = GetOrCreateRecord(wizard, context);

        Reply($"{context.League.m_leagueType}, {context.Season.Template.m_seasonName}: rating {record.Elo}, "
              + $"rank {GetRankName(context.Table, record.RankIndex)} ({record.RankIndex}), "
              + $"{record.Wins} wins, {record.Losses} losses, {record.Draws} draws.");
    }

    private void CommandSetRating(Wizard wizard, LeagueContext context, string value) {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rating)) {
            Reply("The rating must be a whole number.");

            return;
        }

        var record = GetOrCreateRecord(wizard, context);
        var change = PvPEloRecords.SetRating(record, context.Table, rating);
        Finish(wizard, context.Table, change);
        Reply($"Rating set to {change.NewElo}, rank {GetRankName(context.Table, change.NewRankIndex)} ({change.NewRankIndex}).");
    }

    private void CommandSetRank(Wizard wizard, LeagueContext context, string value) {
        if (!TryParseRank(context.Table, value, out var rankIndex)) {
            Reply($"'{value}' is not a rank index from 0 to {context.Table.RankCount - 1} or a rank name.");

            return;
        }

        var record = GetOrCreateRecord(wizard, context);
        var change = PvPEloRecords.SetRank(record, context.Table, rankIndex);
        Finish(wizard, context.Table, change);
        Reply($"Rating set to {change.NewElo}, rank {GetRankName(context.Table, change.NewRankIndex)} ({change.NewRankIndex}).");
    }

    private void CommandSeasonReset(Wizard wizard, LeagueContext context) {
        var record = PvPEloRecords.StartNewSeason(wizard.PvPEloBehavior, context.LeagueId, context.SeasonId, GetSeasonStart(context), context.Table);
        Save(wizard);
        SendEloData(wizard);
        Reply($"A new season record started at rating {record.Elo}, rank {GetRankName(context.Table, record.RankIndex)} ({record.RankIndex}).");
    }

    private void CommandClear(Wizard wizard, LeagueContext context) {
        PvPEloRecords.Clear(wizard.PvPEloBehavior, context.LeagueId);
        Save(wizard);
        SendEloData(wizard);
        Reply($"The ratings of {context.League.m_leagueType} were cleared; the next record starts at {context.Table.StartingElo}.");
    }

    private void Finish(Wizard wizard, PvPRankTable table, PvPEloRecords.Change change) {
        Save(wizard);
        SendEloData(wizard);
        AwardRankBadges(wizard, table, change.NewRankIndex);
    }

    private void AwardRankBadges(Wizard wizard, PvPRankTable table, int rankIndex) {
        foreach (var badgeId in PvPRankBadges.GetAwardIds(table, rankIndex).Where(id => !wizard.BadgeBehavior.Earned.ContainsKey(id))) {
            TellOtherServices(new CHARACTER_103_PROTOCOL.MSG_BADGEEVENT { Kind = BadgeEventKind.Award, BadgeId = badgeId });
        }
    }

    private void CreateMissingRecords(Wizard wizard) {
        var created = false;
        foreach (var context in GetOpenRatedLeagues(wizard)) {
            GetOrCreateRecord(wizard, context, persist: false, out var wasCreated);
            created |= wasCreated;
        }

        if (created) {
            Save(wizard);
        }
    }

    private PvPEloRecord GetOrCreateRecord(Wizard wizard, LeagueContext context)
        => GetOrCreateRecord(wizard, context, persist: true, out _);

    private PvPEloRecord GetOrCreateRecord(Wizard wizard, LeagueContext context, bool persist, out bool created) {
        var record = PvPEloRecords.GetOrCreate(wizard.PvPEloBehavior, context.LeagueId, context.SeasonId, GetSeasonStart(context), context.Table, out created);
        if (created && persist) {
            Save(wizard);
        }

        return record;
    }

    private IEnumerable<LeagueContext> GetOpenRatedLeagues(Wizard wizard) {
        foreach (var league in PvPLeagueFactory.GetLeagues().OrderBy(league => (string) league.m_leagueType, StringComparer.Ordinal)) {
            if (TryCreateContext(league, out var context)
                && PvPEligibility.MeetsLeagueRequirements(league, SessionActor.ActorRef, GetActiveGameObject(), wizard)) {
                yield return context;
            }
        }
    }

    private static bool TryCreateContext(PvPLeagueTemplate league, out LeagueContext context) {
        context = null;
        if (!PvPSeasonClock.IsLeagueOpen(league)
            || !PvPLeagueFactory.TryGetRankTable(league.m_leagueType, out var table)
            || !table.IsRated
            || !PvPSeasonClock.TryGetActiveSeason(league, DateTime.UtcNow, out var season)) {
            return false;
        }

        context = new LeagueContext(league, table, season);

        return true;
    }

    private bool TryResolveCommandLeague(Wizard wizard, string name, out LeagueContext context) {
        context = null;
        if (string.IsNullOrWhiteSpace(name)) {
            context = GetOpenRatedLeagues(wizard).FirstOrDefault();

            return context is not null;
        }

        var league = PvPLeagueFactory.GetLeagues().FirstOrDefault(league => string.Equals(league.m_leagueType, name, StringComparison.OrdinalIgnoreCase));

        return league is not null && TryCreateContext(league, out context);
    }

    private static uint GetSeasonStart(LeagueContext context)
        => PvPSeasonClock.GetStartUnix(context.Season, DateTime.UtcNow);

    private static bool TryParseRank(PvPRankTable table, string value, out int rankIndex) {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rankIndex)) {
            return rankIndex >= 0 && rankIndex < table.RankCount;
        }

        var match = table.Ranks.FirstOrDefault(rank => string.Equals(GetRankName(rank), value, StringComparison.OrdinalIgnoreCase));
        rankIndex = match?.Index ?? -1;

        return match is not null;
    }

    private static string GetRankName(PvPRankTable table, int index)
        => table.GetRank(index) is { } rank ? GetRankName(rank) : index.ToString(CultureInfo.InvariantCulture);

    private static string GetRankName(PvPRankTable.Rank rank) {
        var name = Locale.GetEnglishName(rank.NameKey);

        return string.IsNullOrEmpty(name) ? rank.NameKey : name;
    }

    private void SendEloData(Wizard wizard)
        => SendToSocket(new WIZARD3_56_PROTOCOL.MSG_AdvPvPEloData {
            GlobalID = _eloRequestGlobalId != 0 ? _eloRequestGlobalId : wizard.GameObjectID,
            EntryList = PvPBlobs.EloEntryList(wizard.PvPEloBehavior.Records),
            CheckCompletion = 0,
            RecordCacheID = 0
        });

    private static void Save(Wizard wizard) {
        if (!WizardCollection.UpdateCharacterPvPEloBehavior(wizard)) {
            Logger.Error("Failed to save the PvP ratings of {CharId}.", Logger.Args(wizard.CharId));
        }
    }

    private void Reply(string text)
        => SendToSocket(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE { Message = text, Modal = 0 });

}
