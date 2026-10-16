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
 * BADGE STATE
 * ========================================================================
 *
 * PURPOSE:
 *
 * USAGE EXAMPLE:
 * wizard.BadgeBehavior.Counters[counterKey] = 3;
 *
 * NOTE:
 * Earned is concurrent because purchase gates and requirement handlers on other actors read it while BadgeService writes it. 
 * The three reward and new-badge lists make up the m_badgeData string of the client's BadgeBehavior in the player object.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Behaviors;

public class ServerBadgeBehavior : IClientBehaviorProvider<BadgeBehavior> {

    public const int MAX_NEW_BADGES = 200;

    public static readonly TimeSpan NewBadgeLifetime = TimeSpan.FromHours(48);

    [JsonIgnore] public bool NoTransfer { get; set; } = false;

    public Dictionary<string, ulong> Counters { get; set; } = [];
    public ConcurrentDictionary<uint, EarnedBadge> Earned { get; set; } = [];
    public uint SelectedBadgeId { get; set; }
    public uint NextAwardSequence { get; set; }
    public List<NewBadgeEntry> NewBadges { get; set; } = [];
    public List<uint> ClaimedRewards { get; set; } = [];
    public List<uint> PendingOvercount { get; set; } = [];

    /// <summary>
    /// The new badges the client still lists: not older than <see cref="NewBadgeLifetime"/>, 
    /// at most <see cref="MAX_NEW_BADGES"/> of the newest.
    /// </summary>
    public List<NewBadgeEntry> GetRecentNewBadges(DateTimeOffset now) {
        var oldest = now.Subtract(NewBadgeLifetime).ToUnixTimeSeconds();

        return [.. NewBadges.Where(entry => entry.Time >= oldest)
                            .TakeLast(MAX_NEW_BADGES)];
    }

    public void AddNewBadge(NewBadgeEntry entry, DateTimeOffset now) {
        // The zone service reads these lists while the badge service writes, 
        // so they are replaced, never changed in place.
        NewBadges = [.. NewBadges, entry];
        PruneNewBadges(now);
    }

    public void PruneNewBadges(DateTimeOffset now) => NewBadges = GetRecentNewBadges(now);

    public BadgeBehavior GetClientBehaviorInstance() {
        var badgeData = new BadgeData {
            ClaimedRewards = ClaimedRewards,
            NewBadges = [.. GetRecentNewBadges(DateTimeOffset.UtcNow)
                           .Select(entry => new BadgeData.NewBadge(entry.BadgeId, entry.Time))],
            PendingOvercountRewards = PendingOvercount,
        };

        return new() { m_badgeData = badgeData.ToByteString() };
    }

    public void Reset() {
        Counters.Clear();
        Earned.Clear();
        SelectedBadgeId = 0;
        NextAwardSequence = 0;
        NewBadges = [];
        ClaimedRewards = [];
        PendingOvercount = [];
    }

}
