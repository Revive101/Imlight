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
 * HAS BADGE REQUIREMENT HANDLERS
 * ========================================================================
 *
 * PURPOSE:
 * Answers ReqHasBadge and ReqBaseHasBadge: whether the character has earned the badge the requirement names.
 *
 * USAGE EXAMPLE:
 * Found by reflection in RequirementDispatcher; runs for every requirement list that holds one of the two.
 *
 * NOTE:
 * A badge name the catalog does not know fails the requirement and is warned about once. Under the purchase gate a badge Imlight cannot award counts as met.
 *
 * TODO:
 * Do ReqHasBadge and ReqBaseHasBadge differ? Both are answered the same way.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using System.Collections.Concurrent;
using Imcodec.Cryptography;
using Imcodec.IO;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Badges;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqHasBadgeHandler : BaseRequirementHandler<ReqHasBadge> {

    public override bool Evaluate(IRequirementContext context) => HasBadgeRequirement.IsMet(context, Requirement.m_badgeName, Requirement.m_applyNOT);

}

internal sealed class ReqBaseHasBadgeHandler : BaseRequirementHandler<ReqBaseHasBadge> {

    public override bool Evaluate(IRequirementContext context) => HasBadgeRequirement.IsMet(context, Requirement.m_badgeName, Requirement.m_applyNOT);

}

internal static class HasBadgeRequirement {

    private static readonly ConcurrentDictionary<string, byte> s_warnedNames = new();

    public static bool IsMet(IRequirementContext context, ByteString badgeName, bool applyNot) {
        var wizard = context.GetWizard();
        if (wizard is null || badgeName.IsEmpty) {
            return false;
        }

        string name = badgeName;
        if (context is BadgeGate.GateContext && !BadgeCatalog.Current.IsEarnable(StringHash.Compute(name))) {
            // The dispatcher flips the answer of a NOT requirement, so the answer that lets the sale through depends on it.
            return !applyNot;
        }

        if (!BadgeCatalog.Current.TryGetByName(name, out var definition)) {
            if (s_warnedNames.TryAdd(name, 0)) {
                Logger.Warning("Requirement names badge {0}, which is not in the badge catalog; it fails.", Logger.Args(name));
            }

            return false;
        }

        return BadgeView.IsComplete(wizard.BadgeBehavior, definition);
    }

}
