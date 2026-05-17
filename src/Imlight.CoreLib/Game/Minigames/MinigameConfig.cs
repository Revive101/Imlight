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
 * Last Updated: 3/18/2025
 */

using System.Linq;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Minigames;

/// <summary>
/// Manages minigame configuration loading and provides lookup methods for minigame information.
/// </summary>
internal sealed class MinigameConfig : RootSingleResourceSingleton<MinigameConfig>, IMemoryStreamDisposable {

    protected override string ResourceName => "MinigameConfig.xml";

    private static Imcodec.ObjectProperty.TypeCache.MinigameConfig s_minigameConfig;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        if (!serializer.Deserialize(Stream.ToArray(), out s_minigameConfig)) {
            Logger.Error("Failed to deserialize minigame configuration file");

            return;
        }

        Logger.Information("Loaded {0} minigame configurations",
            Logger.Args(s_minigameConfig.m_minigames.Count));
    }

    public static Imcodec.ObjectProperty.TypeCache.MinigameInfo GetMinigameInfo(int index)
        => s_minigameConfig.m_minigames[index];

    public static string GetMinigameScript(string zoneName) {
        var minigameInfo = s_minigameConfig.m_minigames.FirstOrDefault(x => x.m_zone == zoneName);
        if (minigameInfo == null) {
            Logger.Error("Minigame script not found for zone {0}", 
                Logger.Args(zoneName));

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

}