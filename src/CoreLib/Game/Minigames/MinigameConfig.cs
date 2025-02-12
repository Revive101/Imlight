/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Resources;
using System.Linq;

namespace Imlight.CoreLib.Game.Minigames;

internal sealed class MinigameConfig : RootSingleResourceSingleton<MinigameConfig>, IMemoryStreamDisposable {

    protected override string ResourceName => "MinigameConfig.xml";

    private static TypeCache.MinigameConfig _minigameConfig;

    protected override void AfterLoad() {
        var serializer = new FileSerializer();
        _minigameConfig = serializer.OpenClass<TypeCache.MinigameConfig>(Stream);

        Logger.Information("Loaded {0} minigame configurations", 
            Logger.Args(_minigameConfig.m_minigames.Count));
    }

    public static TypeCache.MinigameInfo GetMinigameInfo(int index) 
        => _minigameConfig.m_minigames[index];
    
    public static string GetMinigameScript(string zoneName) {
        var minigameInfo = _minigameConfig.m_minigames.FirstOrDefault(x => x.m_zone == zoneName);
        if (minigameInfo == null) {
            Logger.Error("Minigame script not found for zone {0}", Logger.Args(zoneName));

            return null;
        }

        return $"{minigameInfo.m_name}/Client.lua";
    }

    public static byte GetMinigameIndex(string zoneName) 
        => (byte) _minigameConfig.m_minigames.FindIndex(x => x.m_zone == zoneName);

    public static bool IsMinigameZone(string zoneName) 
        => _minigameConfig.m_minigames.Any(x => x.m_zone == zoneName);

    public void DisposeStream() 
        => base.Stream.Dispose();

}