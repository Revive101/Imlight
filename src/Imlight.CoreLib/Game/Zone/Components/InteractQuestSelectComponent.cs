/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/21/2025
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractQuestSelectComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName => "QuestSelect";
    public string NpcIcon { get; private set; } = null;
    public string NpcNameKey { get; private set; } = null;
    public string NpcTextKey => "GUI_ChestInteract"; // TODO: Presumably the same for all quest interactables?
                                                     // Come back to this. If we don't see a problem, leave as is and mark as complete.
    public WizBangs WizBang => WizBangs.None;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => null;

    private readonly Dictionary<string, List<ScavengeGoalTemplate>> _scavengeGoalsByQuest = [];

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate
        && template.m_behaviors.Any(x => x is not null && x.m_behaviorName == "WizardSelectBehavior");

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) {
        if (playerCharacter?.QuestBehavior?.CurrentQuestInstances == null) {
            yield break;
        }

        // Only show interaction option if player has an active scavenge goal that matches this object.
        if (HasActiveMatchingScavengeGoal(playerCharacter)) {
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

        // Get goals that use this interact component.
        var questTemplates = QuestTemplateCollection
            .GetAllQuests()
            .Where(x => x is not null)
            .ToList();

        foreach (var qTemplate in questTemplates) {
            foreach (var goal in qTemplate.m_goals.OfType<ScavengeGoalTemplate>()) {
                if (DoesGoalMatchObject(gameObjectTemplate, goal)) {
                    if (!_scavengeGoalsByQuest.TryGetValue(qTemplate.m_questName, out var goalList)) {
                        goalList = [];
                        _scavengeGoalsByQuest[qTemplate.m_questName] = goalList;
                    }
                    goalList.Add(goal);
                }
            }
        }
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        // Find the first active goal that matches this object's adjectives.
        // This ensures we only complete one goal per interaction, even if multiple goals match.
        var activeGoalData = FindActiveMatchingGoal(playerCharacter);
        if (activeGoalData == null) {
            return;
        }

        var (quest, goal, goalProgress) = activeGoalData.Value;

        // Increment the progress for this goal using the proper quest service method.
        playerCharacter.IncrementQuestGoal(quest.QuestName, goal.m_goalName);

        // Check if the goal was completed after incrementing.
        var goalMax = goal.m_tallyCounter?.m_count ?? 1;
        if (goalProgress.CurrentProgress >= goalMax) {
            var goalCompleteMsg = new CHARACTER_103_PROTOCOL.MSG_COMPLETESCAVENGEGOAL {
                QuestID = quest.ID,
                GoalID = goalProgress.ID,
            };
            playerActor.Tell(goalCompleteMsg);
        }

        // Do not delete the object! Sometimes select objects will not be on path,
        // meaning once we destroy them they won't respawn.
        // Instead, let the quest template itself decide how to handle the object post-interaction.
        //Entity.DeleteObject();
    }

    private bool HasActiveMatchingScavengeGoal(Wizard playerCharacter)
        => FindActiveMatchingGoal(playerCharacter) != null;

    private (QuestInstance Quest, ScavengeGoalTemplate Goal, GoalInstance GoalProgress)? FindActiveMatchingGoal(Wizard playerCharacter) {
        var questsWithActiveScavengeGoals = GetQuestsWithActiveScavengeGoals(playerCharacter);
        if (questsWithActiveScavengeGoals == null) {
            return null;
        }

        foreach (var quest in questsWithActiveScavengeGoals) {
            if (!_scavengeGoalsByQuest.TryGetValue(quest.QuestName, out var goals)) {
                continue;
            }

            // Check each matching goal for this quest.
            // Return the first active goal found to ensure only one goal is processed per interaction.
            foreach (var goal in goals) {
                var goalProgress = quest.GoalProgress.FirstOrDefault(gp =>
                    IsActiveScavengeGoal(gp, goal.m_goalName));

                if (goalProgress != null) {
                    return (quest, goal, goalProgress);
                }
            }
        }

        return null;
    }

    private List<QuestInstance> GetQuestsWithActiveScavengeGoals(Wizard playerCharacter)
        => playerCharacter.QuestBehavior?.CurrentQuestInstances?
            .Where(quest => quest.GoalProgress.Any(gp =>
                IsActiveScavengeGoal(gp, null)))
            .ToList();

    private static bool IsActiveScavengeGoal(GoalInstance goalProgress, string goalName)
        => goalProgress.GoalType == GOAL_TYPE.GOAL_TYPE_SCAVENGE
               && goalProgress.CurrentProgress > -1
               && goalProgress.CurrentProgress != int.MaxValue
               && (goalName == null || goalProgress.GoalName == goalName);

    private static bool DoesGoalMatchObject(GameObjectTemplate gameObjectTemplate, ScavengeGoalTemplate goal) {
        // Determine if this goal's scavenge adjectives match any of the object's adjectives.
        var ourAdjs = gameObjectTemplate.m_adjectiveList;
        var goalAdjs = goal.m_itemAdjectives;

        return ourAdjs != null && goalAdjs != null && ourAdjs.Any(goalAdjs.Contains);
    }

}