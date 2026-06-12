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