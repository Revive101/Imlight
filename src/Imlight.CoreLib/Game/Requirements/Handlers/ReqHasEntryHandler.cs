/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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