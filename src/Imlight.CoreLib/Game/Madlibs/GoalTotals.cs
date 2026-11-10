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
 * GOAL TOTALS
 * ========================================================================
 *
 * PURPOSE:
 * Finds how many kills or uses a quest goal needs.
 *
 * USAGE EXAMPLE:
 * var total = GoalTotals.Of(goalTemplate);
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: WonderlandWorkshop with Claude (claude-opus-5-5)
 * Version: KALI 1.0
 * Last Updated: 11/09/2026
 */

using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Madlibs;

internal static class GoalTotals {

    internal static int Of(GoalTemplate goal) {
        // Quest data can leave the tally count at 0 and keep the real total on the goal.
        if (goal.m_tallyCounter is { m_count: > 0 } tally) {
            return tally.m_count;
        }

        return goal switch {
            BountyGoalTemplate bounty => bounty.m_bountyTotal,
            ScavengeGoalTemplate scavenge => scavenge.m_itemTotal,
            _ => 0,
        };
    }

}
