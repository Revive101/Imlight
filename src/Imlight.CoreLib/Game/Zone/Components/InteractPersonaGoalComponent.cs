/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * INTERACT PERSONA GOAL COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages persona goal completion interactions for NPCs that are targets of persona goals.
 * Shows the bright yellow '?' wizbang when a player can complete a persona goal with this NPC.
 * 
 * USAGE EXAMPLE:
 * Automatically attached to NPCs that are referenced as persona goals in quest templates.
 * 
 * NOTE:
 * Only attaches to NPCs that are referenced by name in PersonaGoalTemplate entries.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/22/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal class PlayerPersonaGoalState {

    public bool HasActiveGoal { get; set; }
    public PersonaGoalTemplate ActiveGoal { get; set; }
    public ulong ActiveQuestId { get; set; }
    public ulong ActiveGoalId { get; set; }
    public DateTime LastUpdated { get; set; }

}

internal sealed class InteractPersonaGoalComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const string COMPLETE_NPC_ICON = "Complete";
    private const double WIZBANG_UPDATE_INTERVAL_SECONDS = 2.5;

    public string ServiceName => "QuestPersonaGoalService";
    public string NpcIcon => "";
    public string NpcNameKey => null;
    public string NpcTextKey => null;
    public string StateName => "";
    public string InteractWizBang => "";
    public string DisplayKey => "";

    private WizBangs _wizBang = WizBangs.None;
    public WizBangs WizBang {
        get => _wizBang;
        private set {
            _wizBang = value;
        }
    }

    private readonly Dictionary<string, List<PersonaGoalTemplate>> _personaGoalsByQuest = [];
    private readonly Dictionary<ulong, DateTime> _lastWizBangUpdate = [];
    private readonly Dictionary<ulong, PlayerPersonaGoalState> _cachedPlayerStates = [];
    private string _npcName;

    public static bool ShouldAttachToEntity(CoreTemplate template) {
        if (template is not GameObjectTemplate goTemplate || 
            !template.m_behaviors.Any(x => x is NPCBehaviorTemplate) ||
            template.m_behaviors.Any(x => x is DuelistBehaviorTemplate)) {

            return false;
        }

        var npcName = goTemplate.m_objectName;
        
        // Check if this NPC is referenced as a persona goal target in any quest.
        return QuestTemplateCollection
            .GetAllQuests()
            .Where(x => x is not null)
            .SelectMany(quest => quest.m_goals.OfType<PersonaGoalTemplate>())
            .Any(goal => goal.m_personaName == npcName);
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard wizard) {
        if (wizard is null) {
            yield break;
        }

        var state = GetOrUpdatePlayerState(wizard);
        if (!state.HasActiveGoal || state.ActiveGoal == null) {
            yield break;
        }

        var qTemplate = QuestTemplateCollection.GetQuestByName(
            wizard.QuestBehavior.CurrentQuestInstances
                .FirstOrDefault(q => q.ID == state.ActiveQuestId)?.QuestName);

        if (qTemplate == null) {
            yield break;
        }

        yield return new GoalEntry {
            m_questID = state.ActiveQuestId,
            m_goalID = state.ActiveGoalId,
            m_goalTitle = state.ActiveGoal.m_goalTitle,
            m_questTitle = qTemplate.m_questTitle,
            m_displayKey = qTemplate.m_questTitle,
            m_iconKey = COMPLETE_NPC_ICON,
            m_serviceName = ServiceName,
        };
    }

    public override void OnStart() {
        if (Entity.Template is GameObjectTemplate goTemplate) {
            _npcName = goTemplate.m_objectName;
        }

        // Cache all persona goals that reference this NPC.
        var questTemplates = QuestTemplateCollection
            .GetAllQuests()
            .Where(x => x is not null)
            .ToList();

        foreach (var questTemplate in questTemplates) {
            foreach (var goal in questTemplate.m_goals.OfType<PersonaGoalTemplate>()) {
                if (goal.m_personaName != _npcName) {
                    continue;
                }

                if (!_personaGoalsByQuest.TryGetValue(questTemplate.m_questName, out var value)) {
                    value = [];
                    _personaGoalsByQuest[questTemplate.m_questName] = value;
                }

                value.Add(goal);
            }
        }
    }

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_personaGoalsByQuest.Count <= 0) {
            WizBang = WizBangs.None;

            return;
        }

        var state = GetOrUpdatePlayerState(playerWizard, forceUpdate: true);
        WizBang = state.HasActiveGoal ? WizBangs.CompleteQuestGoal : WizBangs.None;
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (playerWizard is null || _personaGoalsByQuest.Count <= 0) {
            return;
        }

        var now = DateTime.UtcNow;
        var playerId = playerWizard.CharId;

        if (_lastWizBangUpdate.TryGetValue(playerId, out var lastUpdate)) {
            if ((now - lastUpdate).TotalSeconds < WIZBANG_UPDATE_INTERVAL_SECONDS) {
                return;
            }
        }

        _lastWizBangUpdate[playerId] = now;
        var state = GetOrUpdatePlayerState(playerWizard, forceUpdate: true);
        WizBang = state.HasActiveGoal ? WizBangs.CompleteQuestGoal : WizBangs.None;
    }

    private PlayerPersonaGoalState GetOrUpdatePlayerState(Wizard wizard, bool forceUpdate = false) {
        var playerId = wizard.CharId;
        var now = DateTime.UtcNow;

        if (!forceUpdate && _cachedPlayerStates.TryGetValue(playerId, out var cachedState)) {
            if ((now - cachedState.LastUpdated).TotalSeconds < WIZBANG_UPDATE_INTERVAL_SECONDS) {
                return cachedState;
            }
        }

        var state = new PlayerPersonaGoalState { LastUpdated = now };

        var activeGoalResult = FindActivePersonaGoal(wizard);
        if (activeGoalResult.HasValue) {
            state.HasActiveGoal = true;
            state.ActiveGoal = activeGoalResult.Value.Goal;
            state.ActiveQuestId = activeGoalResult.Value.QuestId;
            state.ActiveGoalId = activeGoalResult.Value.GoalId;
        }

        _cachedPlayerStates[playerId] = state;

        return state;
    }

    private (PersonaGoalTemplate Goal, ulong QuestId, ulong GoalId)? FindActivePersonaGoal(Wizard wizard) {
        // Find any of the player's current quests that have cached persona goals for this NPC.
        var relevantQuests = wizard.QuestBehavior.CurrentQuestInstances
            .Where(q => _personaGoalsByQuest.ContainsKey(q.QuestName))
            .ToList();

        // Then, check if any of those goals are active and can be completed with this NPC.
        foreach (var quest in relevantQuests) {
            if (!_personaGoalsByQuest.TryGetValue(quest.QuestName, out var questPersonaGoals)) {
                continue;
            }

            foreach (var goal in questPersonaGoals) {
                var gInstance = quest.GoalProgress.FirstOrDefault(g => g.GoalName == goal.m_goalName);
                if (gInstance is null || !quest.IsGoalActive(goal.m_goalName)) {
                    continue;
                }
                
                return (goal, quest.ID, gInstance.ID);
            }
        }

        return null;
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var state = GetOrUpdatePlayerState(playerCharacter);
        if (state.HasActiveGoal) {
            HandlePersonaGoalCompletion(playerActor, playerCharacter, state);
        }
    }

    private void HandlePersonaGoalCompletion(IActorRef playerActor, Wizard playerCharacter, PlayerPersonaGoalState state) {
        var goalCompleteMsg = new CHARACTER_103_PROTOCOL.MSG_COMPLETEPERSONAGOAL {
            QuestID = state.ActiveQuestId,
            GoalID = state.ActiveGoalId,
        };
        playerActor.Tell(goalCompleteMsg);

        // Invalidate cached state since quest status has changed after goal completion.
        _cachedPlayerStates.Remove(playerCharacter.CharId);

        // Recalculate wizbang immediately to check for new available goals.
        var newState = GetOrUpdatePlayerState(playerCharacter, forceUpdate: true);
        WizBang = newState.HasActiveGoal ? WizBangs.CompleteQuestGoal : WizBangs.None;
    }

}