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
 * Tells which season of a league is running at a given time. A season template stores month, day, hour and
 * minute only; the year is the one of the directory the template was loaded from.
 * 
 * USAGE EXAMPLE:
 * if (PvPSeasonClock.TryGetActiveSeason(league, DateTime.UtcNow, out var season)) { }
 * 
 * NOTE:
 * All times are UTC. A season runs from its start to its end, the end excluded; an end before the start
 * means the season ends in the next year. A season without a year repeats every year.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.PvP.Data;

internal static class PvPSeasonClock {

    public static bool TryGetActiveSeason(PvPLeagueTemplate league, DateTime utcNow, out PvPSeasonEntry season) {
        season = null;
        foreach (string name in league.m_seasonList) {
            if (PvPLeagueFactory.TryGetSeason(name, out var entry) && IsActive(entry, utcNow)) {
                season = entry;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the start of the season in Unix seconds, which is what the client compares with its own season start. A
    /// season that repeats every year starts in the year of the window around the PvP time.
    /// </summary>
    public static uint GetStartUnix(PvPSeasonEntry entry, DateTime utcNow) {
        var window = GetWindows(entry, utcNow)
            .Where(window => window.Start <= utcNow)
            .OrderByDescending(window => window.Start)
            .FirstOrDefault();
        if (window == default) {
            return TryGetWindow(entry, out var start, out _) ? ToUnix(start) : 0;
        }

        return ToUnix(window.Start);
    }

    /// <summary>
    /// Checks that a league is open: one of its seasons is running now.
    /// </summary>
    public static bool IsLeagueOpen(PvPLeagueTemplate league)
        => TryGetActiveSeason(league, DateTime.UtcNow, out _);

    /// <summary>
    /// Checks that a season belongs to a league and is running, or ended within the league grace period.
    /// </summary>
    public static bool IsOpen(PvPLeagueTemplate league, uint seasonId, DateTime utcNow) {
        if (!PvPLeagueFactory.TryGetSeason(seasonId, out var entry)) {
            return false;
        }

        string seasonName = entry.Template.m_seasonName;
        if (!league.m_seasonList.Any(name => (string) name == seasonName)) {
            return false;
        }

        var grace = TimeSpan.FromDays(Math.Max(0, league.m_gracePeriodDays));

        return GetWindows(entry, utcNow).Any(window => utcNow >= window.Start && utcNow < window.End + grace);
    }

    public static bool IsActive(PvPSeasonEntry entry, DateTime utcNow)
        => GetWindows(entry, utcNow).Any(window => utcNow >= window.Start && utcNow < window.End);

    /// <summary>
    /// Gets the one window of a season that has a year.
    /// </summary>
    public static bool TryGetWindow(PvPSeasonEntry entry, out DateTime start, out DateTime end) {
        start = default;
        end = default;

        return entry.Year is { } year && TryBuildWindow(entry.Template, year, out start, out end);
    }

    private static uint ToUnix(DateTime utc)
        => (uint) Math.Max(0, new DateTimeOffset(utc).ToUnixTimeSeconds());

    private static IEnumerable<(DateTime Start, DateTime End)> GetWindows(PvPSeasonEntry entry, DateTime utcNow) {
        int[] years = entry.Year is { } year ? [year] : [utcNow.Year - 1, utcNow.Year];

        foreach (var startYear in years) {
            if (TryBuildWindow(entry.Template, startYear, out var start, out var end)) {
                yield return (start, end);
            }
        }
    }

    private static bool TryBuildWindow(PvPSeasonTemplate template, int startYear, out DateTime start, out DateTime end) {
        end = default;
        if (!TryBuild(startYear, template.m_startMonth, template.m_startDay, template.m_startHour, template.m_startMinute, out start)
            || !TryBuild(startYear, template.m_endMonth, template.m_endDay, template.m_endHour, template.m_endMinute, out end)) {
            return false;
        }

        return end > start
            || TryBuild(startYear + 1, template.m_endMonth, template.m_endDay, template.m_endHour, template.m_endMinute, out end);
    }

    private static bool TryBuild(int year, int month, int day, int hour, int minute, out DateTime result) {
        result = default;
        if (year is < 1 or > 9998 || month is < 1 or > 12 || hour is < 0 or > 23 || minute is < 0 or > 59) {
            return false;
        }

        if (day < 1 || day > DateTime.DaysInMonth(year, month)) {
            return false;
        }

        result = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        return true;
    }

}
