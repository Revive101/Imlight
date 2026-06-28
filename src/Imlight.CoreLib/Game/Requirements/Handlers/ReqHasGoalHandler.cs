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

namespace Imlight.CoreLib.Game.Requirements.Handlers;

internal sealed class ReqHasGoalHandler : BaseRequirementHandler<ReqHasGoal> {

    public override bool Evaluate(IRequirementContext context) {
        var wizard = context.GetWizard();
        if (wizard == null) {
            return false;
        }

        var questName = Requirement.m_questName;
        if (string.IsNullOrEmpty(questName)) {
            return false;
        }

        var goalName = Requirement.m_goalName;
        if (string.IsNullOrEmpty(goalName)) {
            return false;
        }

        var quest = wizard.QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q.QuestName == questName);
        if (quest == null) {
            return false;
        }

        var goal = quest.GoalProgress
            .FirstOrDefault(g => g.GoalName == goalName);
        if (goal == null) {
            return false;
        }

        return Requirement.m_requiredStatus switch {
            GoalStatusRequirement.DontCare => true,
            GoalStatusRequirement.Complete => goal.IsGoalCompleted(),
            GoalStatusRequirement.Incomplete => goal.DoesPlayerHaveGoal() && !goal.IsGoalCompleted(),
            _ => false
        };
    }

}
