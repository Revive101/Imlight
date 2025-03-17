/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Cantrips;

public class CantripFactory : RootSingleResourceSingleton<CantripFactory>, IMemoryStreamDisposable {

    protected override string ResourceName => "CantripXPConfig.xml";

    private static CantripXPConfig s_cantripXPConfig;

    protected override void AfterLoad() {
        var serializer = new BindSerializer();
        var streamAsBytes = Stream.ToArray();
        if (!serializer.Deserialize(streamAsBytes, out s_cantripXPConfig)) {
            Logger.Error("Failed to load CantripXPConfig.xml.");

            return;
        }

        Logger.Information("Loaded {0} cantrip XP levels", Logger.Args(s_cantripXPConfig.m_levelInfo.Count));
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
        if (level < 0 || level > s_cantripXPConfig.m_maxLevel) {
            Logger.Warning("Tried to get cantrip level info for invalid level {0}.", Logger.Args(level));
            return null;
        }

        return s_cantripXPConfig.m_levelInfo[level];
    }

    /// <summary>
    /// Gets the CantripLevelInfo for the specified XP.
    /// </summary>
    /// <param name="xp">The XP of the cantrip.</param>
    /// <returns>The CantripLevelInfo object for the specified XP.</returns>
    public static CantripLevelInfo GetCantripLevelInfoFromXp(int xp) {
        for (var i = 0; i < s_cantripXPConfig.m_maxLevel; i++) {
            if (xp < s_cantripXPConfig.m_levelInfo[i].m_xpToLevel) {
                return s_cantripXPConfig.m_levelInfo[i];
            }
        }

        return s_cantripXPConfig.m_levelInfo[^1];
    }

    public static int GetMaxCantripLevel() => s_cantripXPConfig.m_maxLevel;

    public void DisposeStream() {
        s_cantripXPConfig = null;
        Stream.Dispose();
    }
    
}
