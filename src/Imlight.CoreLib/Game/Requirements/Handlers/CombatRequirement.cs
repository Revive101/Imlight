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
 * COMBAT REQUIREMENT LOOKUPS
 * ========================================================================
 *
 * PURPOSE:
 * Shared lookups the combat requirement handlers use to resolve the participant and count hanging effects.
 *
 * USAGE EXAMPLE:
 * CombatRequirement.GetParticipant(context, Requirement)
 *
 * NOTE:
 * Called by the Req* combat handlers; it is not a handler itself.
 *
 * TODO:
 *
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Combat;
using Imlight.CoreLib.Game.Requirements.Contexts;
using System.Linq;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// Shared lookups of the combat requirement handlers.
/// </summary>
internal static class CombatRequirement {

    private const int ANY_PARAM_LOW = -9999999;
    private const int ANY_PARAM_HIGH = 9999999;

    public static CombatDuelSubCircle GetParticipant(IRequirementContext context, ConditionalSpellEffectRequirement requirement)
        => (context as CombatRequirementContext)?.GetParticipant(requirement);

    public static bool CountInRange(CombatDuelSubCircle participant,
                                    System.Func<SpellEffect, bool> isOfClass,
                                    kHangingDisposition disposition,
                                    int minCount,
                                    int maxCount) {
        var count = participant._hangingEffects.Count(x => isOfClass(x) && CombatHangingEffects.MatchesDisposition(x, disposition));

        return count >= minCount && count <= maxCount;
    }

    public static bool HasEffectType(CombatDuelSubCircle participant, ReqHangingEffectType requirement) {
        var effectType = requirement.m_effectType;
        if (effectType is kSpellEffects.kAfterlife or kSpellEffects.kShadowPact) {
            return false;
        }

        var matching = participant._hangingEffects.Where(x => x.m_effectType == effectType).ToList();
        if (requirement.m_disposition != kHangingDisposition.kBoth) {
            return matching.Any(x => CombatHangingEffects.MatchesDisposition(x, requirement.m_disposition));
        }

        var checkRange = requirement.m_param_low != ANY_PARAM_LOW || requirement.m_param_high != ANY_PARAM_HIGH;
        var count = checkRange
            ? matching.Count(x => x.m_effectParam >= requirement.m_param_low && x.m_effectParam <= requirement.m_param_high)
            : matching.Count;
        if (requirement.m_min_count == 1 && requirement.m_max_count == 100) {
            return count >= 1;
        }

        return count >= requirement.m_min_count && count <= requirement.m_max_count;
    }

}
