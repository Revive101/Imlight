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
 * HANGING EFFECT TYPE REQUIREMENT HANDLER
 * ========================================================================
 *
 * PURPOSE:
 * Answers ReqHangingEffectType: whether the participant carries hanging effects of exactly one type.
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

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// ReqHangingEffectType: the participant carries hanging effects of exactly one type.
/// </summary>
internal sealed class ReqHangingEffectTypeHandler : BaseRequirementHandler<ReqHangingEffectType> {

    public override bool Evaluate(IRequirementContext context) {
        var participant = CombatRequirement.GetParticipant(context, Requirement);

        return participant is not null && CombatRequirement.HasEffectType(participant, Requirement);
    }

}
