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
 * Loads the duel modifier templates (crit rules, banned items, level scaling, turn order) from the
 * game data and looks them up by the name a league, tournament or match lists.
 * 
 * USAGE EXAMPLE:
 * if (DuelModifierFactory.TryGetModifier(name, out var modifier)) { }
 * 
 * NOTE:
 * One game type the tournaments list has no file; the server resolves that name itself (see PvPGameTypes).
 * 
 * TODO:
 * 
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.PvP.Data;

internal sealed class DuelModifierFactory : RootDirectoryResourceSingleton<DuelModifierFactory> {

    protected override string DirectoryName => "DuelModifiers/";

    private static Dictionary<string, DuelModifierTemplate> s_modifiersByName = new(StringComparer.Ordinal);

    protected override void AfterLoad() {
        var byName = new Dictionary<string, DuelModifierTemplate>(StringComparer.Ordinal);

        foreach (var file in base.Files) {
            if (!PvPResourceReader.TryRead<DuelModifierTemplate>(file.Key, file.Value, out var modifier)) {
                continue;
            }

            string name = modifier.m_name;
            if (!byName.TryAdd(name, modifier)) {
                Logger.Error("Duel modifier {Name} in {File} is defined twice.", Logger.Args(name, file.Key.FileName));
            }
        }

        s_modifiersByName = byName;

        Logger.Information("Loaded {Count} duel modifiers.", Logger.Args(byName.Count));
    }

    internal static bool TryGetModifier(string name, out DuelModifierTemplate modifier) {
        modifier = null;

        return name is not null && s_modifiersByName.TryGetValue(name, out modifier);
    }

    internal static IReadOnlyCollection<DuelModifierTemplate> GetModifiers()
        => s_modifiersByName.Values;

}
