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
 * A usage goal's client tags name the object, or a goal tag of one of its interact
 * options (Ddl_WC_DarkCave_Bubble1 on WC_DarkCave_Bubble1), or a global registry entry the option's
 * results remove (a staff whose use removes the entry its goal names).
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/27/2026
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
        => template is GameObjectTemplate gameObjectTemplate
        && (HasBehavior(template, "WizardSelectBehavior") || IsNamedByAnyUsageGoal(gameObjectTemplate));

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
        if (HasActiveMatchingUsageGoal(playerCharacter)
            && Entity.GetComponentOfType<InteractObjectStateComponent>()?.IsInGoalOptionState() != true) {
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
                if (goal.m_goalType != GOAL_TYPE.GOAL_TYPE_USAGE || !DoesGoalMatchObject(gameObjectTemplate, qTemplate.m_questName, goal)) {
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
        // One use completes every active usage goal that matches this object, across all of the
        // player's quests, each through the normal goal-completion path.
        var activeGoals = FindActiveMatchingGoals(playerCharacter);
        if (activeGoals.Count == 0) {
            return;
        }

        var shownDialogs = new HashSet<string>();
        foreach (var (quest, goal, goalProgress) in activeGoals) {
            // Two goals that share a completion dialog show it once.
            var suppressDialog = false;
            var willComplete = goalProgress.CurrentProgress + 1 >= (goal.m_tallyCounter?.m_count ?? 1);
            var dialogKey = CompletionDialogKey(goal);
            if (willComplete && dialogKey is not null) {
                suppressDialog = !shownDialogs.Add(dialogKey);
            }

            // Route the use through the quest service: it increments the tally, reports the
            // new count to the client (progress SENDGOAL), and completes the goal at the cap.
            playerActor.Tell(new CHARACTER_103_PROTOCOL.MSG_COMPLETEUSAGEGOAL {
                QuestID = quest.ID,
                GoalID = goalProgress.ID,
                SuppressCompletionDialog = suppressDialog,
            });
        }

        // The object's own options (a state such as "RedDown", posted events such as "InsertCrystalRed")
        // run when it is used for the goal.
        Entity.GetComponentOfType<InteractObjectStateComponent>()?.ApplyGoalOptions(playerActor, playerObject);

        var goalMax = activeGoals.Max(g => g.Goal.m_tallyCounter?.m_count ?? 1);

        // Collection goals (tally count > 1, e.g. the Triton cogs) consume the object:
        // each use removes that instance from the world. Single-use objects (levers,
        // fairy cages) persist and manage their own post-use state via the goal's
        // completeResults (dyna-mods).
        if (goalMax > 1 && !InteractObjectStateComponent.KeepsStateAfterGoalUse(Entity.Template as GameObjectTemplate)) {
            var leaveServiceRangeMsg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
                MobileID = Entity.ActiveGameObject.m_globalID.Full
            };
            playerActor.Tell(leaveServiceRangeMsg);

            Entity.DeleteObject();
        }
    }

    private bool HasActiveMatchingUsageGoal(Wizard playerCharacter)
        => FindActiveMatchingGoals(playerCharacter).Count > 0;

    private List<(QuestInstance Quest, GoalTemplate Goal, GoalInstance GoalProgress)> FindActiveMatchingGoals(Wizard playerCharacter) {
        var result = new List<(QuestInstance, GoalTemplate, GoalInstance)>();
        var questsWithActiveUsageGoals = GetQuestsWithActiveUsageGoals(playerCharacter);
        if (questsWithActiveUsageGoals == null) {
            return result;
        }

        // Stable order (quest id, then the goal's order in its template) so dialogs play in the same order every time.
        foreach (var quest in questsWithActiveUsageGoals.OrderBy(q => q.ID)) {
            if (!_usageGoalsByQuest.TryGetValue(quest.QuestName, out var goals)) {
                continue;
            }

            foreach (var goal in goals) {
                var goalProgress = quest.GoalProgress.FirstOrDefault(gp =>
                    IsActiveUsageGoal(gp, goal.m_goalName));

                if (goalProgress != null) {
                    result.Add((quest, goal, goalProgress));
                }
            }
        }

        return result;
    }

    private static string CompletionDialogKey(GoalTemplate goal) {
        var dialog = (goal.m_dialogList as ActorDialogList)?.m_dialogs?.FirstOrDefault(d => d.m_dialogTag == "Completion");
        return dialog?.m_dialogEntries is null
            ? null
            : string.Join("|", dialog.m_dialogEntries.Select(e => e.m_dialog));
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

    private static bool HasBehavior(CoreTemplate template, string behaviorName)
        => template.m_behaviors.Any(x => x is not null && x.m_behaviorName == behaviorName);

    private static bool IsNamedByAnyUsageGoal(GameObjectTemplate gameObjectTemplate)
        => QuestTemplateCollection.GetAllQuests()
            .Where(q => q is not null)
            .SelectMany(q => q.m_goals.Where(g => g is not null).Select(g => (q.m_questName, Goal: g)))
            .Any(x => x.Goal.m_goalType == GOAL_TYPE.GOAL_TYPE_USAGE && DoesGoalMatchObject(gameObjectTemplate, x.m_questName, x.Goal));

    private static bool DoesGoalMatchObject(GameObjectTemplate gameObjectTemplate, string questName, GoalTemplate goal) {
        var clientTags = goal.m_clientTags;
        if (clientTags is not null
            && (clientTags.Contains(gameObjectTemplate.m_objectName) || InteractOptionGoalTags(gameObjectTemplate).Any(clientTags.Contains))) {
            return true;
        }

        if (InteractOptions(gameObjectTemplate).Any(option => RequiresGoal(option, questName, goal.m_goalName))) {
            return true;
        }

        return goal is ScavengeGoalTemplate scavengeGoal
            && gameObjectTemplate.m_adjectiveList is not null
            && scavengeGoal.m_itemAdjectives?.Any(gameObjectTemplate.m_adjectiveList.Contains) == true;
    }

    /// <summary>
    /// The tags a usage goal can name an interact option by: the option's goal tags, and the
    /// global registry entries its results remove (a staff whose option removes the entry its goal's client tag names).
    /// </summary>
    internal static IEnumerable<string> OptionTags(InteractOptionTemplate option) {
        if (option is null) {
            yield break;
        }

        foreach (var tag in option.m_goalTags ?? []) {
            yield return tag;
        }

        if (option is not InteractStateOptionTemplate { m_results.m_results: { } results }) {
            yield break;
        }

        foreach (var removal in results.OfType<ResRemoveEntry>().Where(r => !r.m_isQuestRegistry && !string.IsNullOrEmpty(r.m_entryName))) {
            yield return removal.m_entryName;
        }
    }

    /// <summary>
    /// True when some usage goal names this option by one of its tags, so using the option is using that goal.
    /// </summary>
    internal static bool IsNamedByUsageGoal(InteractOptionTemplate option) {
        var tags = OptionTags(option).ToList();
        var quests = QuestTemplateCollection.GetAllQuests().Where(q => q?.m_goals is not null).ToList();

        return (tags.Count > 0
                && quests.SelectMany(q => q.m_goals)
                    .Any(g => g is not null && g.m_goalType == GOAL_TYPE.GOAL_TYPE_USAGE && g.m_clientTags?.Any(tags.Contains) == true))
            || quests.Any(q => q.m_goals.Any(g => g is not null && g.m_goalType == GOAL_TYPE.GOAL_TYPE_USAGE && RequiresGoal(option, q.m_questName, g.m_goalName)));
    }

    private static IEnumerable<string> InteractOptionGoalTags(GameObjectTemplate gameObjectTemplate)
        => InteractOptions(gameObjectTemplate).SelectMany(OptionTags);

    private static IEnumerable<InteractOptionTemplate> InteractOptions(GameObjectTemplate gameObjectTemplate)
        => gameObjectTemplate.m_behaviors
            .OfType<InteractableBehaviorTemplate>()
            .SelectMany(behavior => behavior.m_interactOptions ?? []);

    private static bool RequiresGoal(InteractOptionTemplate option, string questName, string goalName)
        => option is InteractStateOptionTemplate { m_requirements.m_requirements: { } requirements }
            && requirements.OfType<ReqHasGoal>().Any(r => r.m_questName == questName && r.m_goalName == goalName);

}
