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
 * Groups the queued players of a match pool into matches of two sides. Plain logic owned by the PvPDirector: it reads the queue and the
 * match templates, and decides, but changes nothing.
 * 
 * USAGE EXAMPLE:
 * var matches = PvPMatchmaker.FindMatches(queue.Entries, DateTime.UtcNow, charId => isPresent(charId));
 * 
 * NOTE:
 * The rating of practice is the level modifier of the match table at the level of the player. A match takes twice
 * the team size of players. The longest waiting player is grouped with the closest players of the pool inside a
 * range of m_initialQueueThreshold plus m_actorQueueGrowth for every second waited, or with anyone after
 * m_actorQueueTimeout seconds or when they asked to try all opponents. Either player of a pair may be the one the
 * range allows, and every pair of the group must be allowed. The group is split into two sides in snake order of
 * rating (best, then worst and second worst to the other side, and so on) so the side sums stay close. These
 * semantics are inferred from the field names.
 * 
 * TODO:
 * What are m_minQueueSize, m_maxQueueSize and m_queueWaitingTime for?
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.PvP.Data;

namespace Imlight.CoreLib.Game.PvP.Matchmaking;

internal static class PvPMatchmaker {

    /// <summary>
    /// Gets the rating of a level from a level modifier table, interpolating between the listed levels and
    /// clamping to the first and last one.
    /// </summary>
    public static float GetRating(TournamentLevelModifiers table, int level) {
        var points = table.m_levelToRankMod.OrderBy(point => point.X).ToList();
        if (points.Count == 0) {
            return 0;
        }

        if (level <= points[0].X) {
            return points[0].Y;
        }

        for (var i = 1; i < points.Count; i++) {
            if (level > points[i].X) {
                continue;
            }

            var span = points[i].X - points[i - 1].X;
            var fraction = span <= 0 ? 1 : (level - points[i - 1].X) / span;

            return points[i - 1].Y + ((points[i].Y - points[i - 1].Y) * fraction);
        }

        return points[^1].Y;
    }

    /// <summary>
    /// Finds the matches to start now, longest waiting player first. A player is in at most one match. A match is
    /// its two sides of queue entries.
    /// </summary>
    public static List<IReadOnlyList<IReadOnlyList<PvPQueueEntry>>> FindMatches(IEnumerable<PvPQueueEntry> entries, DateTime utcNow,
                                                                                Func<ulong, bool> isAvailable) {
        var matches = new List<IReadOnlyList<IReadOnlyList<PvPQueueEntry>>>();
        var pools = entries
            .Where(entry => isAvailable(entry.CharId))
            .GroupBy(entry => (entry.RealmName, entry.TournamentNameId, entry.MatchNameId, RankedLeagueId: entry.Ranked is null ? 0u : entry.LeagueId));

        foreach (var pool in pools) {
            if (!PvPMatchFactory.TryGetMatch(pool.Key.MatchNameId, out var match) || !PvPMatchFactory.IsSupportedLayout(match)) {
                Logger.Error("Queued match {MatchId} has no template of a supported team layout.", Logger.Args(pool.Key.MatchNameId));

                continue;
            }

            GroupPool(pool, match, utcNow, matches);
        }

        return matches;
    }

    private static void GroupPool(IEnumerable<PvPQueueEntry> pool,
                                  MatchTemplate match,
                                  DateTime utcNow,
                                  List<IReadOnlyList<IReadOnlyList<PvPQueueEntry>>> matches) {
        var groupSize = (int) (match.m_teamSize * match.m_teamCount);
        var waiting = pool.OrderBy(entry => entry.EnteredAtUtc).ThenBy(entry => entry.CharId).ToList();
        var grouped = new HashSet<ulong>();
        foreach (var anchor in waiting) {
            if (grouped.Contains(anchor.CharId)) {
                continue;
            }

            var group = TryBuildGroup(anchor, waiting, grouped, groupSize, match, utcNow);
            if (group is null) {
                continue;
            }

            grouped.UnionWith(group.Select(entry => entry.CharId));
            matches.Add(SplitSides(group));
        }
    }

    private static List<PvPQueueEntry> TryBuildGroup(PvPQueueEntry anchor, List<PvPQueueEntry> waiting, HashSet<ulong> grouped,
                                                     int groupSize, MatchTemplate match, DateTime utcNow) {
        var companions = waiting
            .Where(candidate => candidate.CharId != anchor.CharId && !grouped.Contains(candidate.CharId))
            .Where(candidate => AcceptsEither(match, anchor, candidate, utcNow))
            .OrderBy(candidate => Math.Abs(candidate.Rating - anchor.Rating))
            .Take(groupSize - 1)
            .ToList();
        if (companions.Count < groupSize - 1 || !AllPairsAccepted(companions, match, utcNow)) {
            return null;
        }

        return [anchor, .. companions];
    }

    private static bool AllPairsAccepted(List<PvPQueueEntry> players, MatchTemplate match, DateTime utcNow) {
        for (var i = 0; i < players.Count; i++) {
            for (var j = i + 1; j < players.Count; j++) {
                if (!AcceptsEither(match, players[i], players[j], utcNow)) {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<PvPQueueEntry>[] SplitSides(List<PvPQueueEntry> group) {
        // todo: guess; the best and the worst player share a side, which keeps the rating sums of the sides close.
        var sides = new[] { new List<PvPQueueEntry>(), new List<PvPQueueEntry>() };
        var ordered = group.OrderByDescending(entry => entry.Rating).ThenBy(entry => entry.EnteredAtUtc).ThenBy(entry => entry.CharId);
        var index = 0;
        foreach (var entry in ordered) {
            sides[index % 4 is 0 or 3 ? 0 : 1].Add(entry);
            index++;
        }

        return sides;
    }

    private static bool AcceptsEither(MatchTemplate match, PvPQueueEntry first, PvPQueueEntry second, DateTime utcNow)
        => Accepts(match, first, second, utcNow) || Accepts(match, second, first, utcNow);

    private static bool Accepts(MatchTemplate match, PvPQueueEntry player, PvPQueueEntry opponent, DateTime utcNow) {
        var secondsWaited = Math.Max(0, (utcNow - player.EnteredAtUtc).TotalSeconds);
        if (player.ExpandSearch || secondsWaited >= match.m_actorQueueTimeout) {
            return true;
        }

        var range = match.m_initialQueueThreshold + (match.m_actorQueueGrowth * secondsWaited);

        return Math.Abs(opponent.Rating - player.Rating) <= range;
    }

}
