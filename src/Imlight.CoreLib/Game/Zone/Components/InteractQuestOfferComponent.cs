/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * INTERACT QUEST OFFER COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages quest offer interactions for NPCs that can give quests to players.
 * Shows the bright yellow '!' wizbang when a player can accept a new quest.
 * 
 * USAGE EXAMPLE:
 * Automatically attached to NPCs that give quests based on quest dialog configuration.
 * 
 * NOTE:
 * Only attaches to NPCs that have "Prep" dialogs in quest templates.
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
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal class PlayerQuestOfferState {
    public bool HasAvailableQuest { get; set; }
    public QuestTemplate AvailableQuest { get; set; }
    public DateTime LastUpdated { get; set; }
}

internal sealed class InteractQuestOfferComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const string PREP_NPC_ICON = "Prep";
    private const double WIZBANG_UPDATE_INTERVAL_SECONDS = 2.5;

    public string ServiceName => "QuestOfferService";
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

    private readonly List<QuestTemplate> _givesQuests = [];
    private readonly Dictionary<ulong, DateTime> _lastWizBangUpdate = [];
    private readonly Dictionary<ulong, PlayerQuestOfferState> _cachedPlayerStates = [];

    public static bool ShouldAttachToEntity(CoreTemplate template) {
        if (template is not GameObjectTemplate goTemplate ||
            !template.m_behaviors.Any(x => x is NPCBehaviorTemplate) ||
            template.m_behaviors.Any(x => x is DuelistBehaviorTemplate)) {

            return false;
        }

        // Check if this NPC gives any quests by looking for "Prep" dialogs.
        return QuestTemplateCollection
            .GetAllQuests()
            .Where(x => x?.m_dialogList is ActorDialogList)
            .Any(quest => {
                var dialogList = quest.m_dialogList as ActorDialogList;
                var prepDialogEntry = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");
                var templateId = prepDialogEntry?.m_dialogEntries?.FirstOrDefault()?.m_actorTemplateID;
                return templateId == goTemplate.m_templateID;
            });
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard wizard) {
        if (wizard is null) {
            yield break;
        }

        var state = GetOrUpdatePlayerState(wizard);
        if (!state.HasAvailableQuest || state.AvailableQuest == null) {
            yield break;
        }

        yield return new PrepEntry {
            m_displayKey = state.AvailableQuest.m_questTitle,
            m_iconKey = PREP_NPC_ICON,
            m_serviceName = ServiceName,
        };
    }

    public override void OnStart() {
        // Cache the quests this NPC can give during startup.
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
            }
        }

        _givesQuests.Sort((a, b) => string.Compare(a.m_questTitle, b.m_questTitle, StringComparison.Ordinal));
    }

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_givesQuests.Count <= 0) {
            WizBang = WizBangs.None;

            return;
        }

        var state = GetOrUpdatePlayerState(playerWizard, forceUpdate: true);
        WizBang = state.HasAvailableQuest ? WizBangs.StartQuest : WizBangs.None;
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (playerWizard is null || _givesQuests.Count <= 0) {
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
        WizBang = state.HasAvailableQuest ? WizBangs.StartQuest : WizBangs.None;
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var state = GetOrUpdatePlayerState(playerCharacter);
        if (state.HasAvailableQuest && state.AvailableQuest != null) {
            HandleQuestOffer(playerActor, state.AvailableQuest);
        }
    }

    private PlayerQuestOfferState GetOrUpdatePlayerState(Wizard wizard, bool forceUpdate = false) {
        var playerId = wizard.CharId;
        var now = DateTime.UtcNow;

        if (!forceUpdate && _cachedPlayerStates.TryGetValue(playerId, out var cachedState)) {
            if ((now - cachedState.LastUpdated).TotalSeconds < WIZBANG_UPDATE_INTERVAL_SECONDS) {
                return cachedState;
            }
        }

        var state = new PlayerQuestOfferState { LastUpdated = now };

        // Check if player has any available quests from this NPC.
        foreach (var quest in _givesQuests) {
            var hasQuest = wizard.HasQuest(quest.m_questName);
            var hasCompleted = wizard.HasCompletedQuest(quest.m_questName);

            // Only show quest offer if player doesn't have it and hasn't completed it.
            if (hasQuest || hasCompleted) {
                continue;
            }

            var requirementsMet = quest.m_requirements == null || RequirementDispatcher.EvaluateRequirements(
                requirements: quest.m_requirements,
                context: new QuestRequirementContext(quest.m_requirements, null, null, wizard, quest.m_questName));

            if (requirementsMet) {
                state.HasAvailableQuest = true;
                state.AvailableQuest = quest;

                break; // Take the first available quest.
            }
        }

        _cachedPlayerStates[playerId] = state;

        return state;
    }

    private void HandleQuestOffer(IActorRef playerActor, QuestTemplate quest) {
        SendInteractAvailableQuest(playerActor, quest);
        ShowQuestInfoDialog(playerActor, quest);
        SendQuestOfferDialog(playerActor, quest);
        SendQuestOfferCacheOption(playerActor, quest);
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
                m_goalNameID = goal.m_goalNameID,
                m_goalMadlibs = QuestMadlibs.GetAppropriateMadlibBlockForGoal(goal, null)
            })]
        };

        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(startingGoalCompilation, 1, out var serializedGoals)) {
            Logger.Error("Failed to serialize starting goals for quest {0}.",
                Logger.Args(quest.m_questName));

            return;
        }

        var rewards = GetQuestRewardsFromTemplate(quest, null, playerActor, null);
        if (!serializer.Serialize(rewards, 1, out var serializedRewards)) {
            Logger.Error("Failed to serialize rewards for quest {0}.",
                Logger.Args(quest.m_questName));

            return;
        }

        var questOfferMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_QUESTOFFER {
            MobileID = Entity.ActiveGameObject.m_nMobileID,
            QuestName = quest.m_questName,
            QuestTitle = quest.m_questTitle,
            QuestInfo = "",
            Level = quest.m_questLevel,
            Rewards = serializedRewards,
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

    private static LootInfoList GetQuestRewardsFromTemplate(QuestTemplate qTemplate,
                                                            CoreObject playerObj,
                                                            IActorRef playerActor,
                                                            Wizard playerWizard) {
        if (qTemplate?.m_endResults is null || qTemplate.m_endResults.m_results is null) {
            return new LootInfoList();
        }

        var dropTableResults = qTemplate.m_endResults
            .m_results
            .Where(x => x is ResDropTable);

        var dropTableNames = dropTableResults
            .Select(x => (x as ResDropTable).m_tableName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        var rollResult = DropTableRoller.Roll(dropTableNames, playerActor, playerObj, playerWizard);
        var convertedResults = DropTableConverter.ToLootInfoList(rollResult);

        return convertedResults;
    }

    private void SendInteractAvailableQuest(IActorRef playerActor, QuestTemplate quest) {
        var msg = new WIZARD_12_PROTOCOL.MSG_INTERACTAVAILABLEQUEST {
            MobileID = Entity.ActiveGameObject.m_nMobileID,
            QuestName = quest.m_questName
        };

        playerActor.Tell(msg);
    }

}