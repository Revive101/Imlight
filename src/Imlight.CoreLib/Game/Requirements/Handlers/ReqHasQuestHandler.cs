/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// Handler for ReqHasQuest requirement - checks if player has a specific quest active or completed
/// </summary>
internal sealed class ReqHasQuestHandler : BaseRequirementHandler<ReqHasQuest> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        if (wizard == null) {
            return false;
        }

        var questName = Requirement.m_questName;
        if (string.IsNullOrEmpty(questName)) {
            return false;
        }

        return HasQuestActive(wizard, questName);
    }

    private static bool HasQuestActive(Wizard wizard, string questName) {
        return wizard.QuestBehavior.CurrentQuestInstances
            .Any(q => q.QuestName == questName);
    }


}