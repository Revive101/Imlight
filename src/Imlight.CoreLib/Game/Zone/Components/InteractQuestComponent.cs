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
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractQuestComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName => "QuestingService";
    public string NpcIcon => "Prep";
    public string NpcNameKey => null;
    public string NpcTextKey => null;
    public WizBangs WizBang => WizBangs.StartQuest;
    public string StateName => "";
    public string InteractWizBang => "";
    public string DisplayKey => "";

    private readonly List<QuestTemplate> _availableQuests = [];

    // Always attach since any NPC can be a quest giver.
    // We'll figure out if the quest can be available to the player when the join
    // the zone.
    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate;

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _) {
        // Return a "PrepEntry" service option for each quest that this NPC can give.
        // TODO: filter out the quest requirements here.
        if (_availableQuests.Count == 0) {
            yield break;
        }

        foreach (var quest in _availableQuests) {
            yield return new PrepEntry {
                m_displayKey = quest.m_questTitle,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            };
        }
    }

    public override void OnStart() {
        // Get all of the quests that relate to this NPC.
        var questTemplates = QuestTemplateCollection.GetAllQuests().Where(x => x is not null);

        // Filter the quests to only those that are given by this NPC.
        // We can do that by checking the quest template "Prep" dialog.
        foreach (var questTemplate in questTemplates) {
            var dialogList = questTemplate.m_dialogList as ActorDialogList;
            if (dialogList == null) {
                continue;
            }

            // Get only the dialog entries with "Prep" as the tag, and get the template ID
            // of the first of the first one. If that matches our entity, we know that this
            // NPC gives this quest.
            var prepDialogEntry = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");
            if (prepDialogEntry is null) {
                continue;
            }

            var templateId = prepDialogEntry.m_dialogEntries.FirstOrDefault()?.m_actorTemplateID;
            if (templateId == entity.ActiveGameObject.m_templateID) {
                // This NPC gives this quest.
                _availableQuests.Add(questTemplate);
            }
        }

        _availableQuests.Sort((a, b) => string.Compare(a.m_questTitle, b.m_questTitle, StringComparison.Ordinal));
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var interactedQuest = _availableQuests.ElementAtOrDefault((int) serviceOptionIndex);

        if (interactedQuest is null) {
            Logger.Error("Player {0} attempted to interact with quest NPC {1} for a non-existent quest option index {2}.",
                Logger.Args(playerCharacter.CharId, entity.ActiveGameObject.m_templateID, serviceOptionIndex));

            return;
        }

        // The player has interacted with this quest. If they don't have the quest, show them the quest info dialog.
        // Otherwise, show them the "Underway" dialog.
        if (!playerCharacter.HasQuest(interactedQuest.m_questName) && !playerCharacter.HasCompletedQuest(interactedQuest.m_questName)) {
            ShowQuestInfoDialog(playerActor, interactedQuest);
            SendQuestOfferDialog(playerActor, interactedQuest);
            SendQuestOfferCacheOption(playerActor, interactedQuest);

            return;
        }
        else if (playerCharacter.HasQuest(interactedQuest.m_questName) && !playerCharacter.HasCompletedQuest(interactedQuest.m_questName)) {
            ShowQuestUnderwayDialog(playerActor, interactedQuest);

            return;
        }
    }

    private void ShowQuestInfoDialog(IActorRef playerActor, QuestTemplate quest) {
        if (quest.m_dialogList is not ActorDialogList dialogList) {
            Logger.Error("Quest {0} has no dialog list.",
                Logger.Args(quest.m_questName));

            return;
        }

        var prepDialogList = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");
        if (prepDialogList is null) {
            Logger.Error("Quest {0} has no 'Prep' dialog entry.",
                Logger.Args(quest.m_questName));

            return;
        }

        // Serialize and send the dialog to the player.
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(prepDialogList, 16, out var serializedData)) {
            Logger.Error("Failed to serialize 'Prep' dialog for quest {0}.",
                Logger.Args(quest.m_questName));

            return;
        }

        var dialogMsg = new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = Entity.ActiveGameObject.m_nMobileID,
            CompletionType = "QuestInfo",
            ActorDialog = serializedData,
            Persona = "", // TODO:
            PersonaName = "", // TODO:
            PersonaIcon = "", // TODO:
        };

        playerActor.Tell(dialogMsg);
    }

    private void ShowQuestUnderwayDialog(IActorRef playerActor, QuestTemplate quest) {
        if (quest.m_dialogList is not ActorDialogList dialogList) {
            Logger.Error("Quest {0} has no dialog list.",
                Logger.Args(quest.m_questName));

            return;
        }

        var underwayDialogList = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Underway");
        if (underwayDialogList is null) {
            Logger.Error("Quest {0} has no 'Underway' dialog entry.",
                Logger.Args(quest.m_questName));

            return;
        }

        // Serialize and send the dialog to the player.
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(underwayDialogList, 16, out var serializedData)) {
            Logger.Error("Failed to serialize 'Underway' dialog for quest {0}.",
                Logger.Args(quest.m_questName));

            return;
        }

        var dialogMsg = new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = Entity.ActiveGameObject.m_nMobileID,
            CompletionType = "QuestInfo",
            ActorDialog = serializedData,
            Persona = "", // TODO:
            PersonaName = "", // TODO:
            PersonaIcon = "", // TODO:
        };

        playerActor.Tell(dialogMsg);
    }

    private void SendQuestOfferDialog(IActorRef playerActor, QuestTemplate quest) {
        // Serialize the starting goals for this quest.
        var startingGoals = quest.m_goals
            .Where(g => quest.m_startGoals.Contains(g.m_goalName))
            .ToList();
        var startingGoalCompilation = new GoalCompilation { m_goals = [] };

        foreach (var goal in startingGoals) {
            var goalEntry = new GoalEntryFull {
                m_personaName = "",
                m_goalType = (int) goal.m_goalType,
                m_tallyText = goal.m_goalTitle,
                m_goalLocation = goal.m_locationName,
                m_goalDestinationZone = goal.m_destinationZone,
                m_goalImage1 = goal.m_displayImage1,
                m_goalImage2 = goal.m_displayImage2,
                m_goalNameID = goal.m_goalNameID
            };

            startingGoalCompilation.m_goals.Add(goalEntry);
        }

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
            QuestInfo = "", // ??
            Level = quest.m_questLevel,
            Rewards = "",
            GoalData = serializedGoals,
            Mainline = (byte) (quest.m_mainline ? 1 : 0),
        };

        playerActor.Tell(questOfferMsg);
    }

    private static void SendQuestOfferCacheOption(IActorRef playerActor, QuestTemplate quest) {
        // The SessionActor (aka, the player) might send MSG_ACCEPTQUEST.
        // We can't receive that here, so instead we're going to tell the player
        // that we've sent them a quest offer. A MessageService can cache this request
        // and when they do send that acception, we can process it. 
        var cacheMsg = new CHARACTER_103_PROTOCOL.MSG_SENDQUESTOFFERCACHEOPTION {
            Quest = quest,
        };

        playerActor.Tell(cacheMsg);
    }

}