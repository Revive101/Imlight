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
 * COMBAT HEALTH REQUIREMENT HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Answers ReqCombatHealth: whether the participant's current health over its maximum lies within the fractions.
 *
 * USAGE EXAMPLE:
 * Found by reflection in RequirementDispatcher; runs for every requirement list that holds one.
 *
 * NOTE:
 * The fraction bounds are a guess; see the todo in the handler.
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
/// ReqCombatHealth: the participant's current health over its maximum lies within the fractions.
/// </summary>
internal sealed class ReqCombatHealthHandler : BaseRequirementHandler<ReqCombatHealth> {

    public override bool Evaluate(IRequirementContext context) {
        var stats = CombatRequirement.GetParticipant(context, Requirement)?.ParticipantGameStats;
        if (stats is null || stats.m_baseHitpoints <= 0) {
            return false;
        }

        // todo: the client's health test body was not readable; inclusive bounds on the fraction are a guess.
        var fraction = (float) stats.m_currentHitpoints / stats.m_baseHitpoints;

        return fraction >= Requirement.m_fMinPercent && fraction <= Requirement.m_fMaxPercent;
    }

}
