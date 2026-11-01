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
 * BADGE VIEW
 * ========================================================================
 *
 * PURPOSE:
 * Read-only questions about a character's stored badge state: what is complete, how far a counter is, and which badges the client is shown.
 *
 * USAGE EXAMPLE:
 * var held = BadgeView.GetHeld(wizard.BadgeBehavior, BadgeCatalog.Current);
 *
 * NOTE:
 * A family of tiers shows its completed tiers plus the first incomplete tier once its counter is above zero. NoTitle is always held and complete.
 *
 * TODO:
 * Is the tier visibility rule right? It is inferred from the captured kill messages, not from the client.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Badges;

internal static class BadgeView {

    internal static bool IsComplete(ServerBadgeBehavior state, BadgeDefinition definition)
        => definition.Id == BadgeCatalog.s_noTitleId || state.Earned.ContainsKey(definition.Id);

    internal static ulong GetCounter(ServerBadgeBehavior state, BadgeDefinition definition)
        => state.Counters.GetValueOrDefault(BadgeCatalog.GetCounterKey(definition));

    internal static int GetProgress(ServerBadgeBehavior state, BadgeDefinition definition) {
        if (definition.Max > 0) {
            return IsComplete(state, definition)
                ? definition.Max
                : (int) Math.Min(GetCounter(state, definition), (ulong) definition.Max);
        }

        // A one-shot badge has no target; the client shows the order it was awarded in.
        return state.Earned.TryGetValue(definition.Id, out var earned) ? (int) earned.AwardSequence : 0;
    }

    internal static List<BadgeDefinition> GetHeld(ServerBadgeBehavior state, BadgeCatalog catalog) {
        var held = new HashSet<uint> { BadgeCatalog.s_noTitleId };
        foreach (var id in state.Earned.Keys) {
            held.Add(id);
        }

        foreach (var key in state.Counters.Where(c => c.Value > 0).Select(c => c.Key)) {
            var firstIncomplete = catalog.GetFamily(key).FirstOrDefault(d => !IsComplete(state, d));
            if (firstIncomplete is not null) {
                held.Add(firstIncomplete.Id);
            }
        }

        return [.. catalog.Records.Where(d => held.Contains(d.Id))];
    }

}
