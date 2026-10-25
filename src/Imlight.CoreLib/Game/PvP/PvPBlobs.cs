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
 * Builds the serialized objects the PvP messages carry as byte strings.
 * 
 * USAGE EXAMPLE:
 * var eloList = PvPBlobs.EmptyEloEntryList();
 * 
 * NOTE:
 * Without a PvP packet capture the class project and tournament blobs follow the decompiled client loaders;
 * the empty Elo list equals the capture of a player with no records byte for byte.
 * 
 * TODO:
 * Do the class project blobs need more than the empty lists?
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System.Collections.Generic;
using System.Linq;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP.Data;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.PvP;

internal static class PvPBlobs {

    private const uint CLIENT_PROPERTY_MASK = 4;
    private const uint TOURNAMENT_PROPERTY_MASK = 0x18;

    public static ByteString EmptyProjectProgress()
        => Serialize(new ClassProjectPlayerProgressList { m_playerProgressList = [] });

    public static ByteString EmptyProjectActive()
        => Serialize(new ClassProjectActiveList { m_activeProjectList = [] });

    /// <summary>
    /// The Elo records of a player with none: the list object behind the serializer compress marker bit,
    /// which is clear because the buffer is stored as is.
    /// </summary>
    public static ByteString EmptyEloEntryList()
        => EloEntryList([]);

    /// <summary>
    /// The Elo records of a player, in the form of <see cref="EmptyEloEntryList"/>. Each record is sent as the client
    /// record class, because the client keeps only entries of that class.
    /// </summary>
    public static ByteString EloEntryList(IEnumerable<PvPEloRecord> records) {
        var wrapper = new AdvPvPEloEntryListWrapper { m_entries = [.. records.Select(ToWireRecord)] };
        var list = Serialize(wrapper);

        return new ByteString([0, .. (byte[]) list]);
    }

    /// <summary>
    /// The tournament list of the PvP window: one NewListUpdate as the root, compressed, with its open hosted matches and, because the client takes its teams only from the top level, their teams too. It is
    /// what gives the client tournament info its id, so the window starts asking for updates.
    /// </summary>
    public static ByteString TournamentList(PvPListScope scope) {
        var hosted = PvPRegistry.GetHostedMatches(scope);
        var update = new NewListUpdate {
            m_tournamentName = PvPTournamentFactory.TryGetTournament(scope.TournamentNameId, out var tournament) ? tournament.m_tournamentName : string.Empty,
            m_tournamentNameID = scope.TournamentNameId,
            m_clearData = true,
            m_matches = [.. hosted],
            m_teams = [.. hosted.SelectMany(info => info.m_teams)],
            m_brackets = [],
            m_totalTeams = hosted.Count
        };

        return SerializeCompressed(update);
    }

    public static GAME_5_PROTOCOL.MSG_PVPUPDATEINFO UpdateInfo(ulong charId, ByteString tournamentInfo)
        => new() {
            TournamentInfo = tournamentInfo,
            CharacterID = charId,
            PromptMsg = 0,
            DiffType = 0,
            IsPvPQueue = 0,
            IsPlayerAccountAlreadyHosting = 0
        };

    // The client loads the tournament info with flag 9 (a flags word, then a marker bit and the zlib buffer) and
    // property mask 0x18, and casts the root to NewListUpdate.
    private static AdvPvPEloRecord ToWireRecord(PvPEloRecord record)
        => new AdvPvPEloRecordClient {
            m_eloRecordID = record.RecordId,
            m_leagueID = record.LeagueId,
            m_seasonID = record.SeasonId,
            m_startTime = record.StartTime,
            m_ended = record.Ended,
            m_elo = record.Elo,
            m_rankIndex = record.RankIndex,
            m_wins = record.Wins,
            m_losses = record.Losses,
            m_draws = record.Draws
        };

    private static ByteString SerializeCompressed(PropertyClass value)
        => Serialize(value, SerializerFlags.SerializeFlags | SerializerFlags.Compress, TOURNAMENT_PROPERTY_MASK);

    private static ByteString Serialize(PropertyClass value, SerializerFlags flags = SerializerFlags.None, uint propertyMask = CLIENT_PROPERTY_MASK) {
        var serializer = new ObjectSerializer(Versionable: false, Behaviors: flags);
        if (!serializer.Serialize(value, propertyMask, out var data)) {
            Logger.Error("Failed to serialize PvP blob {Type}.", Logger.Args(value.GetType().Name));

            return default;
        }

        return data;
    }

}
