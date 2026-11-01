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
 * MINIGAME CONFIGURATION MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides centralized configuration loading and retrieval for minigame resources
 * from an configuration file as it is defined in the Root.wad.
 * 
 * USAGE EXAMPLE:
 * var minigameInfo = MinigameConfig.GetMinigameInfo(index);
 * var scriptPath = MinigameConfig.GetMinigameScript(zoneName);
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Minigames;

/// <summary>
/// Manages minigame configuration loading and provides lookup methods for minigame information.
/// </summary>
internal sealed class MinigameConfig : RootSingleResourceSingleton<MinigameConfig>, IMemoryStreamDisposable {

    private const int TIER_COUNT = 12;

    protected override string ResourceName => "MinigameConfig.xml";

    private static Imcodec.ObjectProperty.TypeCache.MinigameConfig s_minigameConfig;
    private static IReadOnlyList<Imcodec.ObjectProperty.TypeCache.MinigameInfo> s_chestGames = [];

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize(Stream.ToArray(), out s_minigameConfig)) {
            throw new InvalidDataException($"The minigame config {ResourceName} could not be read. Re-download the game resources.");
        }

        Logger.Information("Loaded {Count} minigame configurations.",
            Logger.Args(s_minigameConfig.m_minigames.Count));

        var invalidCount = s_minigameConfig.m_minigames.Count(x => !IsValid(x));
        if (invalidCount > 0) {
            Logger.Warning("{Count} minigame configurations are invalid; those chest games are not offered.", Logger.Args(invalidCount));
        }

        s_chestGames = [.. s_minigameConfig.m_minigames.Where(x => x.m_iconArt.IsEmpty && IsValid(x))];
        Logger.Debug("Found {Count} chest games, the configurations without an arcade icon.",
            Logger.Args(s_chestGames.Count));
    }

    public static Imcodec.ObjectProperty.TypeCache.MinigameInfo GetMinigameInfo(int index) {
        var minigames = s_minigameConfig?.m_minigames;
        if (minigames is null || index < 0 || index >= minigames.Count) {
            return null;
        }

        return minigames[index];
    }

    public static bool TryGetMinigameByName(string name, out Imcodec.ObjectProperty.TypeCache.MinigameInfo info) {
        info = s_minigameConfig?.m_minigames?.FirstOrDefault(x => x.m_name == name);

        return info is not null;
    }

    public static bool TryGetRandomChestGame(out Imcodec.ObjectProperty.TypeCache.MinigameInfo info) {
        info = s_chestGames.Count == 0 ? null : s_chestGames[Random.Shared.Next(s_chestGames.Count)];

        return info is not null;
    }

    public static string GetMinigameScript(string zoneName) {
        var minigameInfo = s_minigameConfig.m_minigames.FirstOrDefault(x => x.m_zone == zoneName);
        if (minigameInfo == null) {
            Logger.Error("No minigame is configured for zone {Zone}.", Logger.Args(zoneName));

            return null;
        }

        return $"{minigameInfo.m_name}/Client.lua";
    }

    public static byte GetMinigameIndex(string zoneName)
        => (byte) s_minigameConfig.m_minigames.FindIndex(x => x.m_zone == zoneName);

    public static bool IsMinigameZone(string zoneName)
        => s_minigameConfig.m_minigames.Any(x => x.m_zone == zoneName);

    public void DisposeStream()
        => base.Stream.Dispose();

    private static bool IsValid(Imcodec.ObjectProperty.TypeCache.MinigameInfo minigame) {
        var valid = true;
        var thresholds = minigame.m_scoreThresholds;
        if (thresholds is null || thresholds.Count != TIER_COUNT) {
            Logger.Warning("Minigame {Minigame} has {Count} score thresholds, but needs {Expected}.",
                Logger.Args(minigame.m_name, thresholds?.Count ?? 0, TIER_COUNT));

            valid = false;
        } else if (!IsStrictlyAscending(thresholds)) {
            Logger.Warning("The score thresholds of minigame {Minigame} do not strictly ascend.",
                Logger.Args(minigame.m_name));

            valid = false;
        }

        var rewardTable = minigame.m_rewardTable;
        if (rewardTable is null || rewardTable.Count != TIER_COUNT || rewardTable.Any(x => x.IsEmpty)) {
            Logger.Warning("The reward table of minigame {Minigame} does not have {Expected} named entries.",
                Logger.Args(minigame.m_name, TIER_COUNT));

            valid = false;
        }

        return valid;
    }

    private static bool IsStrictlyAscending(IReadOnlyList<int> values) {
        for (var i = 1; i < values.Count; i++) {
            if (values[i] <= values[i - 1]) {
                return false;
            }
        }

        return true;
    }

}
