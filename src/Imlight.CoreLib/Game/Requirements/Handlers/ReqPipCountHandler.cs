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
 * PIP COUNT REQUIREMENT HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Answers ReqPipCount: whether the participant's pips lie within the range.
 *
 * USAGE EXAMPLE:
 * Found by reflection in RequirementDispatcher; runs for every requirement list that holds one.
 *
 * NOTE:
 * How power pips count is a guess; see the todo in the handler.
 *
 * TODO:
 *
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/22/2026
 */

using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// ReqPipCount: the participant's pips within the range.
/// </summary>
internal sealed class ReqPipCountHandler : BaseRequirementHandler<ReqPipCount> {

    public override bool Evaluate(IRequirementContext context) {
        var participant = CombatRequirement.GetParticipant(context, Requirement);
        var pips = participant?.CombatParticipant?.m_pipCount;
        if (pips is null) {
            return false;
        }

        // todo: how power pips count here is a guess; they count as two generic pips, as an X-pip cost does.
        var total = pips.m_genericPips + (pips.m_powerPips * 2);

        return total >= Requirement.m_minPips && total <= Requirement.m_maxPips;
    }

}
