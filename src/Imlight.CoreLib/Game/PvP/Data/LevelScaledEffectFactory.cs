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
 * Loads the level scaling effect templates from the game effect data: the level an effect scales wizards to, and whether it scales up.
 * 
 * USAGE EXAMPLE:
 * if (LevelScaledEffectFactory.TryGetTemplate(effectName, out var template)) { }
 * 
 * NOTE:
 * The templates sit in the same files as the other game effect templates, so every file of the directory is read and the ones of another kind are skipped.
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/18/2026
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.PvP.Data;

internal sealed class LevelScaledEffectFactory : RootDirectoryResourceSingleton<LevelScaledEffectFactory> {

    protected override string DirectoryName => "GameEffectData/";

    private static Dictionary<string, LevelScaledEffectTemplate> s_templatesByName = new(StringComparer.Ordinal);

    protected override void AfterLoad() {
        var byName = new Dictionary<string, LevelScaledEffectTemplate>(StringComparer.Ordinal);

        foreach (var file in base.Files) {
            if (!PvPResourceReader.TryRead<PropertyClass>(file.Key, file.Value, out var read) || read is not GameEffectTemplateList list) {
                continue;
            }

            foreach (var template in list.m_effectTemplates ?? []) {
                RegisterTemplate(template, file.Key.FileName, byName);
            }
        }

        s_templatesByName = byName;

        Logger.Information("Loaded {Count} level scaling effects.", Logger.Args(byName.Count));
    }

    internal static bool TryGetTemplate(string name, out LevelScaledEffectTemplate template) {
        template = null;

        return name is not null && s_templatesByName.TryGetValue(name, out template);
    }

    private static void RegisterTemplate(GameEffectTemplate template, string fileName, Dictionary<string, LevelScaledEffectTemplate> byName) {
        if (template is not LevelScaledEffectTemplate scaled) {
            return;
        }

        string name = scaled.m_effectName;
        if (!byName.TryAdd(name, scaled)) {
            Logger.Error("Level scaling effect {Name} in {File} is defined twice.", Logger.Args(name, fileName));
        }
    }

}
