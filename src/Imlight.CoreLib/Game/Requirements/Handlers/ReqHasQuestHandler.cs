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

using System.Linq;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Requirements.Handlers;

/// <summary>
/// Handler for ReqHasQuest requirement - checks if player has a specific quest active or completed
/// </summary>
internal sealed class ReqHasQuestHandler : BaseRequirementHandler<ReqHasQuest> {

    private const string QUEST_COMPLETED_ENTRY = "Complete";

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        if (wizard == null) {
            return false;
        }

        var questName = Requirement.m_questName;
        if (string.IsNullOrEmpty(questName)) {
            return false;
        }

        return HasQuestActiveOrCompleted(wizard, questName);
    }

    private static bool HasQuestActiveOrCompleted(Wizard wizard, string questName) {
        // Completion drops the instance from the journal, so a completed quest is only visible
        // through the entry CompleteQuest stamps. Client data pairs this with a
        // ReqHasEntry on that same entry, which no player could satisfy if this were
        // limited to active quests.
        return wizard.QuestBehavior.CurrentQuestInstances
            .Any(q => q.QuestName == questName)
            || wizard.HasQuestRegistryValue(questName, QUEST_COMPLETED_ENTRY);
    }


}