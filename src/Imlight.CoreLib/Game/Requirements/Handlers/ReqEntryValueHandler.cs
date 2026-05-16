/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqEntryValueHandler : BaseRequirementHandler<ReqEntryValue> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        if (wizard == null) {
            return false;
        }

        var entryName = Requirement.m_entryName;
        if (string.IsNullOrEmpty(entryName)) {
            return false;
        }

        var requiredValue = Requirement.m_numericValue;
        var operatorType = Requirement.m_operatorType;

        float actualValue = GetEntryValue(wizard, entryName);

        return operatorType switch {
            OPERATOR_TYPE.OPERATOR_EQUALS => actualValue == requiredValue,
            OPERATOR_TYPE.OPERATOR_GREATER_THAN => actualValue > requiredValue,
            OPERATOR_TYPE.OPERATOR_LESS_THAN => actualValue < requiredValue,
            OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ => actualValue >= requiredValue,
            OPERATOR_TYPE.OPERATOR_LESS_THAN_EQ => actualValue <= requiredValue,
            _ => false
        };
    }

    private float GetEntryValue(Wizard wizard, string entryName) {
        if (Requirement.m_isQuestRegistry) {
            return wizard.GetQuestRegistryValue(Requirement.m_questName, entryName);
        }
        else {
            return wizard.GetRegistryValue(entryName);
        }
    }

}