/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Cantrips;

public class CantripFactory : RootSingleResourceSingleton<CantripFactory>, IMemoryStreamDisposable {
    protected override string ResourceName => "CantripXPConfig.xml";

    private static CantripXPConfig _cantripXPConfig;

    protected override void AfterLoad() {
        var serializer = new FileSerializer();
        _cantripXPConfig = serializer.OpenClass<CantripXPConfig>(Stream);

        Logger.Information("Loaded {0} cantrip XP levels", Logger.Args(_cantripXPConfig.m_levelInfo.Count));
    }

    /// <summary>
    /// Creates a cantrip template from a template ID.
    /// </summary>
    /// <param name="templateId">The ID of the cantrip template.</param>
    /// <returns>The created cantrip template object.</returns>
    public static CantripsSpellTemplate CreateCantripTemplateFromId(uint templateId) {
        var template = (CantripsSpellTemplate) CoreObjectFactory.GetCoreTemplate(templateId);

        if (template == null) {
            Logger.Warning("Tried to create cantrip from non-existent template {0}.", Logger.Args(templateId));
            return null;
        }
        if (template is not CantripsSpellTemplate cantripSpellTemplate) {
            Logger.Warning("Tried to create cantrip from non-cantrip template {0}.", Logger.Args(templateId));
            return null;
        }

        return cantripSpellTemplate;
    }

    /// <summary>
    /// Gets the CantripLevelInfo for the specified level.
    /// </summary>
    /// <param name="level">The level of the cantrip.</param>
    /// <returns>The CantripLevelInfo object for the specified level, or null if the level is invalid.</returns>
    public static CantripLevelInfo GetCantripLevelInfo(int level) {
        if (level < 0 || level > _cantripXPConfig.m_maxLevel) {
            Logger.Warning("Tried to get cantrip level info for invalid level {0}.", Logger.Args(level));
            return null;
        }

        return _cantripXPConfig.m_levelInfo[level];
    }

    /// <summary>
    /// Gets the CantripLevelInfo for the specified XP.
    /// </summary>
    /// <param name="xp">The XP of the cantrip.</param>
    /// <returns>The CantripLevelInfo object for the specified XP.</returns>
    public static CantripLevelInfo GetCantripLevelInfoFromXp(int xp) {
        for (var i = 0; i < _cantripXPConfig.m_maxLevel; i++) {
            if (xp < _cantripXPConfig.m_levelInfo[i].m_xpToLevel) {
                return _cantripXPConfig.m_levelInfo[i];
            }
        }

        return _cantripXPConfig.m_levelInfo[^1];
    }

    public static int GetMaxCantripLevel() => _cantripXPConfig.m_maxLevel;

    public void DisposeStream() {
        _cantripXPConfig = null;
        Stream.Dispose();
    }
}
