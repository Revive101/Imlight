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
 * REQUIREMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Evaluates ReqMagicLevel: the level of the player against a number.
 * 
 * USAGE EXAMPLE:
 * Evaluated by RequirementDispatcher; quests and the PvP leagues list it as their entry level.
 *
 * NOTE:
 * Handlers are discovered by reflection. The client compares the level of the player's school behavior and never
 * reads the school the requirement names (ReqMagicLevel::GetCurrentValue 0x14213ab50), so the school is ignored here too.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/14/2026
 */

using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqMagicLevelHandler : BaseRequirementHandler<ReqMagicLevel> {

    public override bool Evaluate(IRequirementContext context) {
        var magicSchool = context.GetWizard()?.MagicSchoolBehavior;
        if (magicSchool is null) {
            return false;
        }

        return NumericRequirement.Compare(Requirement, magicSchool.Level);
    }

}
