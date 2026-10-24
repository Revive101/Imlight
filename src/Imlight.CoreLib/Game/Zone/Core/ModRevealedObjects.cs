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
 * MOD REVEALED OBJECTS
 * ========================================================================
 *
 * PURPOSE:
 * Boot-time, data-derived index of the placed objects a quest result reveals with a dynamic mod
 * (ResAddDynaMod that shows its object). Such an object is hidden from a player until a quest shows it.
 *
 * USAGE EXAMPLE:
 * var hiddenUntilShown = ModRevealedObjects.Contains(zonePath, zoneTag);
 *
 * NOTE:
 * Several placements can share one spot, each shown by a different quest stage, so an object that
 * every player was sent would stand in the world more than once.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Results;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Zone.Core;

internal static class ModRevealedObjects {

    private static readonly Lazy<HashSet<string>> s_revealed = new(Build);

    public static bool Contains(string zonePath, string zoneTag)
        => !string.IsNullOrEmpty(zoneTag) && s_revealed.Value.Contains(Key(zonePath, zoneTag));

    public static bool Reveals(ResAddDynaMod mod)
        => mod.m_bShowsObject && !mod.m_dynaModRemove;

    private static HashSet<string> Build()
        => QuestResultScan.Find<ResAddDynaMod>(QuestTemplateCollection.GetAllQuests())
            .Where(Reveals)
            .Select(mod => Key(mod.m_zoneName.ToString(), mod.m_dynaModClientTag.ToString()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Key(string zonePath, string zoneTag)
        => $"{zonePath}|{zoneTag}";

}
