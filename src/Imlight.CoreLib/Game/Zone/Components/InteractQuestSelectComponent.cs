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
 * INTERACT QUEST SELECT COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Handles a quest goal where the player's goal is to interact with a
 * specific object in the game world.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Collection goals (tally count > 1) consume the object on use; single-use goals
 * leave the object in place and drive post-use state via completeResults.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 08/22/2026
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractQuestSelectComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName => "Interact";
    public string NpcIcon { get; private set; } = null;
    public string NpcNameKey { get; private set; } = null;
    public string NpcTextKey => "GUI_ChestInteract"; // TODO: Presumably the same for all quest interactables?
                                                     // Come back to this. If we don't see a problem, leave as is and mark as complete.
    public WizBangs WizBang => WizBangs.None;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => null;

    private readonly Dictionary<string, List<GoalTemplate>> _usageGoalsByQuest = [];

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate
        && template.m_behaviors.Any(x => x is not null && x.m_behaviorName == "WizardSelectBehavior");

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) {
        if (playerCharacter?.QuestBehavior?.CurrentQuestInstances == null) {
            yield break;
        }

        // If we did not find any quests with active usage goals, don't show the interaction option at all.
        if (_usageGoalsByQuest.Count == 0) {
            yield break;
        }

        // If there are no quests with active usage goals, don't show the interaction option at all.
        var questsWithActiveUsageGoals = GetQuestsWithActiveUsageGoals(playerCharacter);
        if (questsWithActiveUsageGoals is null || questsWithActiveUsageGoals.Count == 0) {
            yield break;
        }

        // Only show interaction option if player has an active usage goal that matches this object.
        if (HasActiveMatchingUsageGoal(playerCharacter)) {
            yield return new InteractableOption { m_serviceName = ServiceName };
        }
    }

    public override void OnStart() {
        if (Entity.Template is not GameObjectTemplate gameObjectTemplate) {
            return;
        }

        // Determine icon and name key from template.
        NpcIcon = gameObjectTemplate.m_sIcon;
        NpcNameKey = gameObjectTemplate.m_displayName;

        // Register every usage goal whose client tags name this object; the object is
        // interactable only while one of those goals is active (see GetServiceOptions).
        foreach (var qTemplate in QuestTemplateCollection.GetAllQuests()) {
            if (qTemplate is null) {
                continue;
            }

            foreach (var goal in qTemplate.m_goals) {
                if (goal.m_goalType != GOAL_TYPE.GOAL_TYPE_USAGE || !DoesGoalMatchObject(gameObjectTemplate, goal)) {
                    continue;
                }

                if (!_usageGoalsByQuest.TryGetValue(qTemplate.m_questName, out var goalList)) {
                    goalList = [];
                    _usageGoalsByQuest[qTemplate.m_questName] = goalList;
                }
                goalList.Add(goal);
            }
        }
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        // Find the first active goal that matches this object's client tags.
        // This ensures we only complete one goal per interaction, even if multiple goals match.
        var activeGoalData = FindActiveMatchingGoal(playerCharacter);
        if (activeGoalData == null) {
            return;
        }

        var (quest, goal, goalProgress) = activeGoalData.Value;

        // Route the use through the quest service: it increments the tally, reports the
        // new count to the client (progress SENDGOAL), and completes the goal at the cap.
        var goalCompleteMsg = new CHARACTER_103_PROTOCOL.MSG_COMPLETEUSAGEGOAL {
            QuestID = quest.ID,
            GoalID = goalProgress.ID,
        };
        playerActor.Tell(goalCompleteMsg);

        var goalMax = goal.m_tallyCounter?.m_count ?? 1;

        // Collection goals (tally count > 1, e.g. the Triton cogs) consume the object:
        // each use removes that instance from the world. Single-use objects (levers,
        // fairy cages) persist and manage their own post-use state via the goal's
        // completeResults (dyna-mods).
        if (goalMax > 1) {
            var leaveServiceRangeMsg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
                MobileID = Entity.ActiveGameObject.m_globalID.Full
            };
            playerActor.Tell(leaveServiceRangeMsg);

            Entity.DeleteObject();
        }
    }

    private bool HasActiveMatchingUsageGoal(Wizard playerCharacter)
        => FindActiveMatchingGoal(playerCharacter) != null;

    private (QuestInstance Quest, GoalTemplate Goal, GoalInstance GoalProgress)? FindActiveMatchingGoal(Wizard playerCharacter) {
        var questsWithActiveUsageGoals = GetQuestsWithActiveUsageGoals(playerCharacter);
        if (questsWithActiveUsageGoals == null) {
            return null;
        }

        foreach (var quest in questsWithActiveUsageGoals) {
            if (!_usageGoalsByQuest.TryGetValue(quest.QuestName, out var goals)) {
                continue;
            }

            // Check each matching goal for this quest.
            // Return the first active goal found to ensure only one goal is processed per interaction.
            foreach (var goal in goals) {
                var goalProgress = quest.GoalProgress.FirstOrDefault(gp =>
                    IsActiveUsageGoal(gp, goal.m_goalName));

                if (goalProgress != null) {
                    return (quest, goal, goalProgress);
                }
            }
        }

        return null;
    }

    private static List<QuestInstance> GetQuestsWithActiveUsageGoals(Wizard playerCharacter)
        => playerCharacter.QuestBehavior?.CurrentQuestInstances?
            .Where(quest => quest.GoalProgress.Any(gp =>
                IsActiveUsageGoal(gp, null)))
            .ToList();

    private static bool IsActiveUsageGoal(GoalInstance goalProgress, string goalName)
        => goalProgress.GoalType == GOAL_TYPE.GOAL_TYPE_USAGE
               && goalProgress.CurrentProgress > -1
               && goalProgress.CurrentProgress != int.MaxValue
               && (goalName == null || goalProgress.GoalName == goalName);

    private static bool DoesGoalMatchObject(GameObjectTemplate gameObjectTemplate, GoalTemplate goal)
        => goal.m_clientTags?.Contains(gameObjectTemplate.m_objectName) == true;

}