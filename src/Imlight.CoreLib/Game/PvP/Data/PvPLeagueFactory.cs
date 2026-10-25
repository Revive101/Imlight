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
 * Loads the league templates, the season templates and the Elo rank table from the PvP directory of the
 * game data, and looks leagues and seasons up by name or by the hash the client sends.
 * 
 * USAGE EXAMPLE:
 * if (PvPLeagueFactory.TryGetLeague(message.LeagueID, out var league)) { }
 * 
 * NOTE:
 * League and season ids are StringHash.Compute of m_leagueType and m_seasonName.
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
using System.Text.RegularExpressions;
using Imcodec.Cryptography;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.PvP.Data;

internal sealed partial class PvPLeagueFactory : RootDirectoryResourceSingleton<PvPLeagueFactory> {

    protected override string DirectoryName => "PvP/";

    private static Dictionary<string, PvPLeagueTemplate> s_leaguesByName = new(StringComparer.Ordinal);
    private static Dictionary<uint, PvPLeagueTemplate> s_leaguesById = [];
    private static Dictionary<string, PvPSeasonEntry> s_seasonsByName = new(StringComparer.Ordinal);
    private static Dictionary<uint, PvPSeasonEntry> s_seasonsById = [];
    private static EloConfig s_eloConfig;
    private static PvPRankTable s_defaultRankTable;
    private static Dictionary<string, PvPRankTable> s_rankTablesByLeagueType = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"/Seasons(?<year>\d{4})/", RegexOptions.CultureInvariant)]
    private static partial Regex SeasonYearPattern();

    [GeneratedRegex(@"_(?<month>\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex SeasonNameMonthPattern();

    protected override void AfterLoad() {
        var leaguesByName = new Dictionary<string, PvPLeagueTemplate>(StringComparer.Ordinal);
        var leaguesById = new Dictionary<uint, PvPLeagueTemplate>();
        var seasonsByName = new Dictionary<string, PvPSeasonEntry>(StringComparer.Ordinal);
        var seasonsById = new Dictionary<uint, PvPSeasonEntry>();
        EloConfig eloConfig = null;

        foreach (var file in base.Files) {
            if (!PvPResourceReader.TryRead<PropertyClass>(file.Key, file.Value, out var template)) {
                continue;
            }

            switch (template) {
                case PvPLeagueTemplate league:
                    RegisterLeague(league, file.Key.FileName, leaguesByName, leaguesById);

                    break;
                case PvPSeasonTemplate season:
                    RegisterSeason(season, file.Key.FileName, seasonsByName, seasonsById);

                    break;
                case EloConfig elo:
                    if (eloConfig is not null) {
                        Logger.Error("The Elo config in {File} is a second one.", Logger.Args(file.Key.FileName));
                    }

                    eloConfig = elo;

                    break;
                default:
                    Logger.Error("PvP file {File} is a {Type}, which no loader handles.",
                        Logger.Args(file.Key.FileName, template.GetType().Name));

                    break;
            }
        }

        s_leaguesByName = leaguesByName;
        s_leaguesById = leaguesById;
        s_seasonsByName = seasonsByName;
        s_seasonsById = seasonsById;
        s_eloConfig = eloConfig;
        BuildRankTables(eloConfig, leaguesByName.Keys);

        Logger.Information("Loaded {LeagueCount} PvP leagues and {SeasonCount} seasons.", 
            Logger.Args(leaguesByName.Count, seasonsByName.Count));
        if (eloConfig is null) {
            Logger.Error("The PvP Elo config is missing from the game data.");
        }

    }

    private static void BuildRankTables(EloConfig eloConfig, IEnumerable<string> leagueTypes) {
        var byLeagueType = new Dictionary<string, PvPRankTable>(StringComparer.OrdinalIgnoreCase);
        s_defaultRankTable = null;
        if (eloConfig is null) {
            s_rankTablesByLeagueType = byLeagueType;

            return;
        }

        s_defaultRankTable = CreateRankTable(eloConfig.m_defaultRatingsTable);
        foreach (var template in eloConfig.m_perLeagueRatingsTables ?? []) {
            var table = CreateRankTable(template);
            if (!byLeagueType.TryAdd(table.LeagueType, table)) {
                Logger.Error("The Elo config has two rating tables for the league type {LeagueType}.", Logger.Args(table.LeagueType));
            }
        }

        s_rankTablesByLeagueType = byLeagueType;
        LogRankTables(leagueTypes);
    }

    private static PvPRankTable CreateRankTable(EloRatingsTable template) {
        var ranks = (template.m_ranks ?? []).Select((rank, index) => new PvPRankTable.Rank(
            Index: index,
            Down: rank.m_eloRankDown,
            Up: rank.m_eloRankUp,
            KFactor: rank.m_kFactor,
            Carryover: rank.m_carryoverElo,
            NameKey: rank.m_rankName.ToString(),
            BadgeName: rank.m_badgeName.ToString()));

        return new PvPRankTable(template.m_leagueType.ToString(), template.m_startingElo, template.m_minElo, ranks);
    }

    private static void LogRankTables(IEnumerable<string> leagueTypes) {
        var leagues = leagueTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var table in s_rankTablesByLeagueType.Values) {
            Logger.Information("PvP rating table for league type {LeagueType}: {RankCount} ranks, start {Start}, floor {Floor}, rated {IsRated}, league exists {LeagueExists}.",
                Logger.Args(table.LeagueType, table.RankCount, table.StartingElo, table.MinElo, table.IsRated, leagues.Contains(table.LeagueType)));
        }

        var withoutTable = leagues.Where(league => !s_rankTablesByLeagueType.ContainsKey(league)).ToList();
        if (withoutTable.Count > 0) {
            Logger.Information("PvP leagues without their own rating table, which use the default one: {Leagues}.", Logger.Args(string.Join(", ", withoutTable)));
        }
    }

    private static void RegisterLeague(PvPLeagueTemplate league,
                                       string fileName,
                                       Dictionary<string, PvPLeagueTemplate> byName,
                                       Dictionary<uint, PvPLeagueTemplate> byId) {
        string name = league.m_leagueType;
        if (!byName.TryAdd(name, league) || !byId.TryAdd(StringHash.Compute(name), league)) {
            Logger.Error("League {Name} in {File} is defined twice.", Logger.Args(name, fileName));
        }
    }

    private static void RegisterSeason(PvPSeasonTemplate season,
                                       string fileName,
                                       Dictionary<string, PvPSeasonEntry> byName,
                                       Dictionary<uint, PvPSeasonEntry> byId) {
        string name = season.m_seasonName;
        var entry = new PvPSeasonEntry(season, GetStartYear(season, fileName));
        if (!byName.TryAdd(name, entry) || !byId.TryAdd(StringHash.Compute(name), entry)) {
            Logger.Error("Season {Name} in {File} is defined twice.", Logger.Args(name, fileName));
        }
    }

    private static int? GetStartYear(PvPSeasonTemplate season, string fileName) {
        var yearMatch = SeasonYearPattern().Match(fileName ?? string.Empty);
        if (!yearMatch.Success) {
            return null;
        }

        var directoryYear = int.Parse(yearMatch.Groups["year"].Value, CultureInfo.InvariantCulture);

        // The name carries the month the season starts in, so a season named for January starts in late December of the year before.
        var monthMatch = SeasonNameMonthPattern().Match(season.m_seasonName.ToString());
        if (monthMatch.Success && season.m_startMonth - int.Parse(monthMatch.Groups["month"].Value, CultureInfo.InvariantCulture) > 6) {
            return directoryYear - 1;
        }

        return directoryYear;
    }

    internal static bool TryGetLeague(string name, out PvPLeagueTemplate league) {
        league = null;

        return name is not null && s_leaguesByName.TryGetValue(name, out league);
    }

    internal static bool TryGetLeague(uint leagueId, out PvPLeagueTemplate league)
        => s_leaguesById.TryGetValue(leagueId, out league);

    internal static bool TryGetSeason(string name, out PvPSeasonEntry season) {
        season = null;

        return name is not null && s_seasonsByName.TryGetValue(name, out season);
    }

    internal static bool TryGetSeason(uint seasonId, out PvPSeasonEntry season)
        => s_seasonsById.TryGetValue(seasonId, out season);

    internal static bool TryGetEloConfig(out EloConfig eloConfig) {
        eloConfig = s_eloConfig;

        return eloConfig is not null;
    }

    internal static bool TryGetRankTable(string leagueType, out PvPRankTable table) {
        table = null;
        if (leagueType is not null && s_rankTablesByLeagueType.TryGetValue(leagueType, out table)) {
            return true;
        }

        table = s_defaultRankTable;

        return table is not null;
    }

    internal static IReadOnlyCollection<PvPRankTable> GetRankTables()
        => s_rankTablesByLeagueType.Values;

    internal static PvPRankTable GetDefaultRankTable()
        => s_defaultRankTable;

    internal static IReadOnlyCollection<PvPLeagueTemplate> GetLeagues()
        => s_leaguesByName.Values;

    internal static IReadOnlyCollection<PvPSeasonEntry> GetSeasons()
        => s_seasonsByName.Values;

}
