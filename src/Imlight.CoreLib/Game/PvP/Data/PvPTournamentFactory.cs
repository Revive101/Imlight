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
 * Loads the tournament templates (practice, ranked, bracket and pet tournaments) from the game data
 * and looks them up by name or by the name hash the client sends as TournamentNameID.
 * 
 * USAGE EXAMPLE:
 * if (PvPTournamentFactory.TryGetTournament(message.TournamentNameID, out var tournament)) { }
 * 
 * NOTE:
 * The id the client sends is StringHash.Compute of m_tournamentName.
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
using Imcodec.Cryptography;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.PvP.Data;

internal sealed class PvPTournamentFactory : RootDirectoryResourceSingleton<PvPTournamentFactory> {

    protected override string DirectoryName => "Tournaments/";

    private static Dictionary<string, TournamentTemplate> s_tournamentsByName = new(StringComparer.Ordinal);
    private static Dictionary<uint, TournamentTemplate> s_tournamentsById = [];

    protected override void AfterLoad() {
        var byName = new Dictionary<string, TournamentTemplate>(StringComparer.Ordinal);
        var byId = new Dictionary<uint, TournamentTemplate>();

        foreach (var file in base.Files) {
            if (!PvPResourceReader.TryRead<TournamentTemplate>(file.Key, file.Value, out var tournament)) {
                continue;
            }

            string name = tournament.m_tournamentName;
            if (!byName.TryAdd(name, tournament) || !byId.TryAdd(StringHash.Compute(name), tournament)) {
                Logger.Error("Tournament {Name} in {File} is defined twice.", Logger.Args(name, file.Key.FileName));
            }
        }

        s_tournamentsByName = byName;
        s_tournamentsById = byId;

        Logger.Information("Loaded {Count} PvP tournaments.", Logger.Args(byName.Count));
        Logger.Information("PvP ranked play uses the tournament {Tournament}; {RankedCount} tournament(s) of the data update a ladder with the Elo behavior.",
            Logger.Args(TryGetRankedTournament(out var ranked) ? ranked.m_tournamentName.ToString() : "none", byName.Values.Count(IsRanked)));
    }

    internal static bool TryGetTournament(string name, out TournamentTemplate tournament) {
        tournament = null;

        return name is not null && s_tournamentsByName.TryGetValue(name, out tournament);
    }

    internal static bool TryGetTournament(uint nameId, out TournamentTemplate tournament)
        => s_tournamentsById.TryGetValue(nameId, out tournament);

    internal static bool IsRanked(TournamentTemplate tournament)
        => tournament.m_useEloBehavior && tournament.m_updateLadder && !tournament.m_isBracketTournament;

    internal static bool TryGetRankedTournament(out TournamentTemplate tournament) {
        tournament = s_tournamentsByName.Values
            .Where(IsRanked)
            .OrderBy(candidate => (string) candidate.m_tournamentName, StringComparer.Ordinal)
            .FirstOrDefault();

        return tournament is not null;
    }

    internal static bool IsPractice(TournamentTemplate tournament)
        => !tournament.m_updateLadder && !tournament.m_isBracketTournament && tournament.m_pvpDuelModifiers?.Any() == true;

    internal static bool TryGetPracticeTournament(out TournamentTemplate tournament) {
        tournament = s_tournamentsByName.Values
            .Where(IsPractice)
            .OrderBy(candidate => (string) candidate.m_tournamentName, StringComparer.Ordinal)
            .FirstOrDefault();

        return tournament is not null;
    }

    internal static IReadOnlyCollection<TournamentTemplate> GetTournaments()
        => s_tournamentsByName.Values;

}
