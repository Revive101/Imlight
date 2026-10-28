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
 * Evaluates ReqIsGender: whether the gender chosen at character creation is the one the requirement names.
 *
 * USAGE EXAMPLE:
 * Evaluated by RequirementDispatcher; dorm exits list it to pick the dorm a player comes out of.
 *
 * NOTE:
 * The gender is matched by name, ignoring case. A name that is not a gender fails the requirement.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/21/2026
 */

using System;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqIsGenderHandler : BaseRequirementHandler<ReqIsGender> {

    public override bool Evaluate(IRequirementContext context) {
        var avatar = context.GetWizard()?.WizardAvatar;
        if (avatar is null) {
            return false;
        }

        if (!Enum.TryParse<eGender>(Requirement.m_playerGender, true, out var gender)) {
            return false;
        }

        return avatar.m_eGender == gender;
    }

}
