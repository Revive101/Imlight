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

internal sealed class ReqHasEntryHandler : BaseRequirementHandler<ReqHasEntry> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        if (wizard == null) {
            return false;
        }

        var questName = Requirement.m_questName;
        if (string.IsNullOrEmpty(questName)) {
            return false;
        }

        var entryName = Requirement.m_entryName;
        if (string.IsNullOrEmpty(entryName)) {
            return false;
        }

        return GetEntryValue(wizard, questName, entryName);
    }

    private bool GetEntryValue(Wizard wizard, string questName, string entryName) {
        if (Requirement.m_isQuestRegistry) {
            return wizard.HasQuestRegistryValue(questName, entryName);
        }
        else {
            return wizard.HasRegistryValue(entryName);
        }
    }

}