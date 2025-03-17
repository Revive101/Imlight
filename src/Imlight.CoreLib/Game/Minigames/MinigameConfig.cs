/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imcodec.ObjectProperty;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Minigames;

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
            Logger.Error("Minigame script not found for zone {0}", Logger.Args(zoneName));

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