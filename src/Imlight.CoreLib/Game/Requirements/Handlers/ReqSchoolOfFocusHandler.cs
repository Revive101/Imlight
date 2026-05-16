/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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