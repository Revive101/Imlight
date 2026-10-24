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
 * HANGING CHARM REQUIREMENT HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Answers ReqHangingCharm: whether the participant's charms of the disposition number within the range.
 *
 * USAGE EXAMPLE:
 * Found by reflection in RequirementDispatcher; runs for every requirement list that holds one.
 *
 * NOTE:
 * Answers false when the context is not a combat context.
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

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// ReqHangingCharm: the participant's charms of the disposition number within the range.
/// </summary>
internal sealed class ReqHangingCharmHandler : BaseRequirementHandler<ReqHangingCharm> {

    public override bool Evaluate(IRequirementContext context) {
        var participant = CombatRequirement.GetParticipant(context, Requirement);
        if (participant is null) {
            return false;
        }

        return CombatRequirement.CountInRange(participant, CombatHangingEffects.IsCharm,
            Requirement.m_disposition, Requirement.m_minCount, Requirement.m_maxCount);
    }

}
