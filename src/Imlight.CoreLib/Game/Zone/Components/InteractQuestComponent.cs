/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * INTERACT QUEST COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages interaction logic for quest NPCs in the game world,
 * handling player service interactions and state transitions.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/11/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal enum QuestInteractionType {

    None,
    QuestOffer,
    QuestUnderway,
    GoalCompletion

}

internal class PlayerQuestState {

    public QuestInteractionType InteractionType { get; set; } = QuestInteractionType.None;
    public WizBangs WizBang { get; set; } = WizBangs.None;
    public QuestTemplate AvailableQuest { get; set; }
    public PersonaGoalTemplate ActiveGoal { get; set; }
    public ulong ActiveQuestId { get; set; }
    public ulong ActiveGoalId { get; set; }
    public DateTime LastUpdated { get; set; }

}

internal sealed class InteractQuestComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const string PREP_NPC_ICON = "Prep";
    private const string COMPLETE_NPC_ICON = "Complete";
    private const double WIZBANG_UPDATE_INTERVAL_SECONDS = 2.5;

    public string ServiceName => "QuestingService";
    public string NpcIcon => "";
    public string NpcNameKey => null;
    public string NpcTextKey => null;
    public string StateName => "";
    public string InteractWizBang => "";
    public string DisplayKey => "";

    private WizBangs _wizBang = WizBangs.StartQuest;
    public event System.Action WizBangChanged;
    public WizBangs WizBang {
        get => _wizBang;
        private set {
            if (_wizBang != value) {
                _wizBang = value;
                WizBangChanged?.Invoke();
            }
        }
    }

    private readonly List<QuestTemplate> _givesQuests = [];
    private readonly List<PersonaGoalTemplate> _personaGoals = [];
    private readonly Dictionary<ulong, DateTime> _lastWizBangUpdate = [];
    private readonly Dictionary<ulong, PlayerQuestState> _cachedPlayerStates = [];
    private string _npcName;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate
        && template.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && !template.m_behaviors.Any(x => x is DuelistBehaviorTemplate);

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard wizard) {
        if (wizard is null || (_givesQuests.Count <= 0 && _personaGoals.Count <= 0)) {
            yield break;
        }

        var state = GetOrUpdatePlayerState(wizard);

        switch (state.InteractionType) {
            case QuestInteractionType.QuestOffer:
                yield return new PrepEntry {
                    m_displayKey = state.AvailableQuest.m_questTitle,
                    m_iconKey = PREP_NPC_ICON,
                    m_serviceName = ServiceName,
                };
                break;

            case QuestInteractionType.QuestUnderway:
                yield return new PrepEntry {
                    m_displayKey = state.AvailableQuest.m_questTitle,
                    m_iconKey = PREP_NPC_ICON,
                    m_serviceName = ServiceName,
                };
                break;

            case QuestInteractionType.GoalCompletion:
                var qTemplate = QuestTemplateCollection.GetQuestByName(
                    wizard.QuestBehavior.CurrentQuestInstances
                        .FirstOrDefault(q => q.ID == state.ActiveQuestId)?.QuestName);

                if (qTemplate != null) {
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
                break;
        }
    }

    public override void OnStart() {
        if (Entity.Template is GameObjectTemplate goTemplate) {
            _npcName = goTemplate.m_objectName;
        }

        var questTemplates = QuestTemplateCollection
            .GetAllQuests()
            .Where(x => x is not null)
            .ToList();

        foreach (var questTemplate in questTemplates) {
            if (questTemplate.m_dialogList is not ActorDialogList dialogList) {
                continue;
            }

            var prepDialogEntry = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");
            var templateId = prepDialogEntry?.m_dialogEntries?.FirstOrDefault()?.m_actorTemplateID;

            if (templateId == entity.ActiveGameObject.m_templateID) {
                _givesQuests.Add(questTemplate);

                continue;
            }

            foreach (var goal in questTemplate.m_goals.OfType<PersonaGoalTemplate>()) {
                if (goal.m_personaName == _npcName) {
                    _personaGoals.Add(goal);
                }
            }
        }

        _givesQuests.Sort((a, b) => string.Compare(a.m_questTitle, b.m_questTitle, StringComparison.Ordinal));
    }

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_givesQuests.Count <= 0 && _personaGoals.Count <= 0) {
            WizBang = WizBangs.None;

            return;
        }

        // Force update on zone join to catch goal state changes from other zones.
        var state = GetOrUpdatePlayerState(playerWizard, forceUpdate: true);
        WizBang = state.WizBang;
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (playerWizard is null || _givesQuests.Count <= 0 && _personaGoals.Count <= 0) {
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
        WizBang = state.WizBang;
    }

    private PlayerQuestState GetOrUpdatePlayerState(Wizard wizard, bool forceUpdate = false) {
        var playerId = wizard.CharId;
        var now = DateTime.UtcNow;

        if (!forceUpdate && _cachedPlayerStates.TryGetValue(playerId, out var cachedState)) {
            if ((now - cachedState.LastUpdated).TotalSeconds < WIZBANG_UPDATE_INTERVAL_SECONDS) {
                return cachedState;
            }
        }

        var state = new PlayerQuestState { LastUpdated = now };

        var activeGoalResult = FindActivePersonaGoal(wizard);
        if (activeGoalResult.HasValue) {
            state.InteractionType = QuestInteractionType.GoalCompletion;
            state.WizBang = WizBangs.CompleteQuestGoal;
            state.ActiveGoal = activeGoalResult.Value.Goal;
            state.ActiveQuestId = activeGoalResult.Value.QuestId;
            state.ActiveGoalId = activeGoalResult.Value.GoalId;
        }
        else {
            var questResult = EvaluateQuestOffering(wizard);
            state.InteractionType = questResult.Type;
            state.WizBang = questResult.WizBang;
            state.AvailableQuest = questResult.Quest;
        }

        _cachedPlayerStates[playerId] = state;

        return state;
    }

    private (PersonaGoalTemplate Goal, ulong QuestId, ulong GoalId)? FindActivePersonaGoal(Wizard wizard) {
        var relevantQuest = wizard.QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q.GoalProgress
                .Any(g => _personaGoals.Any(pg => pg.m_goalName == g.GoalName && pg.m_personaName == _npcName)));

        if (relevantQuest == null) {
            return null;
        }

        foreach (var goal in _personaGoals) {
            var gInstance = relevantQuest.GoalProgress.FirstOrDefault(g => g.GoalName == goal.m_goalName);
            if (gInstance != null && relevantQuest.IsGoalActive(goal.m_goalName) && goal.m_personaName == _npcName) {
                return (goal, relevantQuest.ID, gInstance.ID);
            }
        }

        return null;
    }

    private (QuestInteractionType Type, WizBangs WizBang, QuestTemplate Quest) EvaluateQuestOffering(Wizard wizard) {
        foreach (var quest in _givesQuests) {
            var hasQuest = wizard.HasQuest(quest.m_questName);
            var hasCompleted = wizard.HasCompletedQuest(quest.m_questName);

            if (hasQuest && !hasCompleted) {
                return (QuestInteractionType.QuestUnderway, WizBangs.UnfinishedQuest, quest);
            }

            if (!hasQuest && !hasCompleted) {
                var requirementsMet = quest.m_requirements == null || RequirementDispatcher.EvaluateRequirements(
                    requirements: quest.m_requirements,
                    context: new QuestRequirementContext(quest.m_requirements, null, null, wizard, quest.m_questName));

                if (requirementsMet) {
                    return (QuestInteractionType.QuestOffer, WizBangs.StartQuest, quest);
                }
            }
        }

        return (QuestInteractionType.None, WizBangs.None, null);
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var state = GetOrUpdatePlayerState(playerCharacter);

        switch (state.InteractionType) {
            case QuestInteractionType.GoalCompletion:
                HandleGoalCompletion(playerActor, playerCharacter, state);
                break;

            case QuestInteractionType.QuestOffer:
                HandleQuestOffer(playerActor, state.AvailableQuest);
                break;

            case QuestInteractionType.QuestUnderway:
                HandleQuestUnderway(playerActor, state.AvailableQuest);
                break;
        }
    }

    private void HandleGoalCompletion(IActorRef playerActor, Wizard playerCharacter, PlayerQuestState state) {
        var questTemplate = QuestTemplateCollection.GetQuestByName(
            playerCharacter.QuestBehavior.CurrentQuestInstances
                .FirstOrDefault(q => q.ID == state.ActiveQuestId)?.QuestName);

        if (questTemplate == null) {
            Logger.Error("Player {0} has quest instance for unknown quest with ID '{1}'.",
                Logger.Args(playerCharacter.CharId, state.ActiveQuestId));

            return;
        }

        ShowQuestGoalCompletionDialog(playerActor, questTemplate, state.ActiveGoal, state.ActiveQuestId, state.ActiveGoalId);

        // Invalidate cached state since quest status has changed after goal completion.
        _cachedPlayerStates.Remove(playerCharacter.CharId);

        // Recalculate wizbang immediately to check for new available quests.
        var newState = GetOrUpdatePlayerState(playerCharacter, forceUpdate: true);
        WizBang = newState.WizBang;
    }

    private void HandleQuestOffer(IActorRef playerActor, QuestTemplate quest) {
        ShowQuestInfoDialog(playerActor, quest);
        SendQuestOfferDialog(playerActor, quest);
        SendQuestOfferCacheOption(playerActor, quest);
    }

    private void HandleQuestUnderway(IActorRef playerActor, QuestTemplate quest) {
        ShowQuestUnderwayDialog(playerActor, quest);
    }

    private void ShowQuestInfoDialog(IActorRef playerActor, QuestTemplate quest) {
        var dialogList = quest.m_dialogList as ActorDialogList;
        var prepDialogList = dialogList?.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");

        if (prepDialogList == null) {
            Logger.Error("Quest {0} has no 'Prep' dialog entry.",
                Logger.Args(quest.m_questName));

            return;
        }

        SendActorDialog(playerActor, prepDialogList, "QuestInfo");
    }

    private void ShowQuestUnderwayDialog(IActorRef playerActor, QuestTemplate quest) {
        var dialogList = quest.m_dialogList as ActorDialogList;
        var underwayDialogList = dialogList?.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Underway");

        if (underwayDialogList == null) {
            Logger.Error("Quest {0} has no 'Underway' dialog entry.",
                Logger.Args(quest.m_questName));

            return;
        }

        SendActorDialog(playerActor, underwayDialogList, "QuestInfo");
    }

    private void ShowQuestGoalCompletionDialog(IActorRef playerActor, QuestTemplate quest, GoalTemplate goal, ulong questId, ulong goalId) {
        var dialogList = quest.m_dialogList as ActorDialogList;
        var completeDialogList = dialogList?.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Completion");

        if (completeDialogList == null) {
            Logger.Error("Quest {0} has no 'Completion' dialog entry.",
                Logger.Args(quest.m_questName));

            return;
        }

        var goalCompleteMsg = new WIZARD_12_PROTOCOL.MSG_INTERACTCOMPLETEGOAL {
            MobileID = new GID(Entity.ActiveGameObject.m_nMobileID),
            QuestID = questId,
            GoalID = goalId,
        };
        playerActor.Tell(goalCompleteMsg);

        SendActorDialog(playerActor, completeDialogList, "Completion", questId, goalId);
        WizBang = WizBangs.None;
    }

    private void SendActorDialog(IActorRef playerActor, ActorDialog dialogEntry, string completionType, ulong questId = 0, ulong goalId = 0) {
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(dialogEntry, 16, out var serializedData)) {
            Logger.Error("Failed to serialize '{0}' dialog.",
                Logger.Args(completionType));

            return;
        }

        var dialogMsg = new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = questId > 0 ? new GID(Entity.ActiveGameObject.m_nMobileID) : Entity.ActiveGameObject.m_nMobileID,
            QuestID = questId,
            GoalID = goalId,
            CompletionType = completionType,
            ActorDialog = serializedData,
            Persona = "",
            PersonaName = "",
            PersonaIcon = "",
        };

        playerActor.Tell(dialogMsg);
    }

    private void SendQuestOfferDialog(IActorRef playerActor, QuestTemplate quest) {
        var startingGoals = quest.m_goals
            .Where(g => quest.m_startGoals.Contains(g.m_goalName))
            .ToList();

        var startingGoalCompilation = new GoalCompilation {
            m_goals = [.. startingGoals.Select(goal => new GoalEntryFull {
                m_personaName = "",
                m_goalType = (int) goal.m_goalType,
                m_tallyText = goal.m_goalTitle,
                m_goalLocation = goal.m_locationName,
                m_goalDestinationZone = goal.m_destinationZone,
                m_goalImage1 = goal.m_displayImage1,
                m_goalImage2 = goal.m_displayImage2,
                m_goalNameID = goal.m_goalNameID
            })]
        };

        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(startingGoalCompilation, 1, out var serializedGoals)) {
            Logger.Error("Failed to serialize starting goals for quest {0}.",
                Logger.Args(quest.m_questName));

            return;
        }

        var questOfferMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_QUESTOFFER {
            MobileID = Entity.ActiveGameObject.m_nMobileID,
            QuestName = quest.m_questName,
            QuestTitle = quest.m_questTitle,
            QuestInfo = "",
            Level = quest.m_questLevel,
            Rewards = "",
            GoalData = serializedGoals,
            Mainline = (byte) (quest.m_mainline ? 1 : 0),
        };

        playerActor.Tell(questOfferMsg);
    }

    private static void SendQuestOfferCacheOption(IActorRef playerActor, QuestTemplate quest) {
        var cacheMsg = new CHARACTER_103_PROTOCOL.MSG_SENDQUESTOFFERCACHEOPTION {
            Quest = quest,
        };

        playerActor.Tell(cacheMsg);
    }

}