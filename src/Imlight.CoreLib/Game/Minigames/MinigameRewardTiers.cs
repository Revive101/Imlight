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
 * MINIGAME REWARD TIERS
 * ========================================================================
 *
 * PURPOSE:
 * Maps a minigame score to the client's threshold index, the drop tables that index rolls,
 * and the number of reward slots the rewards window shows for it.
 *
 * USAGE EXAMPLE:
 * var k = MinigameRewardTiers.GetThresholdIndex(info.m_scoreThresholds, score);
 * var tables = MinigameRewardTiers.GetLootTableNames(info.m_rewardTable, k);
 *
 * NOTE:
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Generic;

namespace Imlight.CoreLib.Game.Minigames;

internal static class MinigameRewardTiers {

    private const int ThresholdsPerSlot = 3;
    private const int FirstBonusTable = 2;
    private const int SecondBonusTable = 5;
    private const int ThirdBonusTable = 8;

    internal static int GetThresholdIndex(IReadOnlyList<int> thresholds, int score) {
        // -1 below the first threshold, the last index at or above the last one.
        for (var index = thresholds.Count - 1; index >= 0; index--) {
            if (thresholds[index] <= score) {
                return index;
            }
        }

        return -1;
    }

    internal static IReadOnlyList<string> GetLootTableNames(IReadOnlyList<string> rewardTable, int thresholdIndex) {
        if (thresholdIndex < 0 || thresholdIndex >= rewardTable.Count) {
            return [];
        }

        // Every third tier adds the table at 2, 5 and 8 below the tier's own table.
        var names = new List<string>(4);
        if (thresholdIndex > FirstBonusTable) {
            names.Add(rewardTable[FirstBonusTable]);
        }

        if (thresholdIndex > SecondBonusTable) {
            names.Add(rewardTable[SecondBonusTable]);
        }

        if (thresholdIndex > ThirdBonusTable) {
            names.Add(rewardTable[ThirdBonusTable]);
        }

        names.Add(rewardTable[thresholdIndex]);

        return names;
    }

    internal static int SlotCount(int thresholdIndex)
        => thresholdIndex < 0 ? 0 : thresholdIndex / ThresholdsPerSlot + 1;

    internal static int FinalPhaseForSlots(int slots)
        => slots == 0 ? 0 : ThresholdsPerSlot * slots - 1;

}
