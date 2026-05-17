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
 */

using System;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// Handler for ReqIsSchool requirement - checks if player is in a specific school
/// </summary>
internal sealed class ReqSchoolOfFocusHandler : BaseRequirementHandler<ReqSchoolOfFocus> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        if (wizard == null) {
            return false;
        }

        var schoolName = Requirement.m_magicSchool;
        if (string.IsNullOrEmpty(schoolName)) {
            return false;
        }

        return IsInSchool(wizard, schoolName);
    }
    
    private static bool IsInSchool(Wizard wizard, string schoolName) {
        if (wizard?.MagicSchoolBehavior?.MagicSchool == null) {
            return false;
        }

        return string.Equals(
            wizard.MagicSchoolBehavior.MagicSchool.ToString(),
            schoolName,
            StringComparison.OrdinalIgnoreCase
        );
    }

}