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
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractQuestComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const string PREP_NPC_ICON = "Prep";
    private const string COMPLETE_NPC_ICON = "Complete";

    public string ServiceName => "QuestingService";
    public string NpcIcon => "";
    public string NpcNameKey => null;
    public string NpcTextKey => null;
    public WizBangs WizBang => WizBangs.StartQuest;
    public string StateName => "";
    public string InteractWizBang => "";
    public string DisplayKey => "";

    private readonly List<QuestTemplate> _givesQuests = [];
    private readonly List<PersonaGoalTemplate> _personaGoals = [];

    // Always attach since any NPC can be a quest giver.
    // We'll figure out if the quest can be available to the player when the join
    // the zone.
    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate
        && template.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && !template.m_behaviors.Any(x => x is DuelistBehaviorTemplate);

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard wizard) {
        if (wizard is null) {
            yield break;
        }

        if (_givesQuests.Count <= 0 && _personaGoals.Count <= 0) {
            yield break;
        }

        foreach (var quest in _givesQuests) {
            // If the player hasn't completed this quest and doesn't have it, show it.
            // TODO: check requirements.
            if (!wizard.HasQuest(quest.m_questName) || !wizard.HasCompletedQuest(quest.m_questName)) {
                yield return new PrepEntry {
                    m_displayKey = quest.m_questTitle,
                    m_iconKey = PREP_NPC_ICON,
                    m_serviceName = ServiceName,
                };
            }
        }

        // If the player has this quest but has not completed it,
        // check if any of their active goals are a persona goal for this NPC.
        if (Entity.Template is not GameObjectTemplate goTemplate) {
            yield break;
        }

        var myName = goTemplate.m_objectName;
        var relevantQuests = wizard.QuestBehavior.CurrentQuestInstances
            .Where(q => q.GoalProgress
                .Any(g => _personaGoals.Any(pg => pg.m_goalName == g.GoalName && pg.m_personaName == myName)))
            .ToList();

        // If they do have the relevant quest, check to see if they have an active goal for this NPC.
        foreach (var quest in relevantQuests) {
            var qTemplate = QuestTemplateCollection.GetQuestByName(quest.QuestName);
            if (qTemplate is null) {
                Logger.Error("Player {0} has quest instance for unknown quest '{1}'.",
                    Logger.Args(wizard.CharId, quest.QuestName));

                continue;
            }

            foreach (var goal in _personaGoals) {
                // Get the player's instance of this goal.
                var gInstance = quest.GoalProgress
                    .FirstOrDefault(g => g.GoalName == goal.m_goalName);
                if (gInstance is null) {
                    Logger.Error("Player {0} has quest {1} but is missing goal {2}.",
                        Logger.Args(wizard.CharId, quest.QuestName, goal.m_goalName));

                    continue;
                }

                if (quest.IsGoalActive(goal.m_goalName) && goal.m_personaName == myName) {
                    yield return new GoalEntry {
                        m_questID = quest.ID,
                        m_goalID = gInstance.ID,
                        m_goalTitle = goal.m_goalTitle,
                        m_questTitle = qTemplate.m_questTitle,
                        m_displayKey = qTemplate.m_questTitle,
                        m_iconKey = COMPLETE_NPC_ICON,
                        m_serviceName = ServiceName,
                    };

                    break;
                }
            }
        }
    }

    public override void OnStart() {
        // Get all of the quests that relate to this NPC.
        var questTemplates = QuestTemplateCollection
            .GetAllQuests()
            .Where(x => x is not null)
            .ToList();

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
            if (prepDialogEntry is null || prepDialogEntry.m_dialogEntries == null || prepDialogEntry.m_dialogEntries.Count == 0) {
                continue;
            }

            var templateId = prepDialogEntry.m_dialogEntries.FirstOrDefault()?.m_actorTemplateID;
            if (templateId == entity.ActiveGameObject.m_templateID) {
                // This NPC gives this quest.
                _givesQuests.Add(questTemplate);

                return;
            }

            // Check to see if any of the persona goals for this quest template are me.
            foreach (var goal in questTemplate.m_goals) {
                if (goal is not PersonaGoalTemplate personaGoal) {
                    continue;
                }

                if (entity.Template is not GameObjectTemplate goTemplate) {
                    continue;
                }

                if (personaGoal.m_personaName == goTemplate.m_objectName) {
                    // This NPC is a persona goal for this quest.
                    _personaGoals.Add(personaGoal);
                }
            }
        }

        _givesQuests.Sort((a, b) => string.Compare(a.m_questTitle, b.m_questTitle, StringComparison.Ordinal));
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        if (Entity.Template is not GameObjectTemplate goTemplate) {
            return;
        }

        var myName = goTemplate.m_objectName;

        // First, check if player has an active goal for this NPC (persona goal takes priority).
        var relevantQuest = playerCharacter.QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q.GoalProgress
                .Any(g => _personaGoals.Any(pg => pg.m_goalName == g.GoalName && pg.m_personaName == myName)));

        if (relevantQuest != null) {
            var questTemplate = QuestTemplateCollection.GetQuestByName(relevantQuest.QuestName);
            if (questTemplate is null) {
                Logger.Error("Player {0} has quest instance for unknown quest '{1}'.",
                    Logger.Args(playerCharacter.CharId, relevantQuest.QuestName));
                    
                return;
            }

            foreach (var goal in _personaGoals) {
                var gInstance = relevantQuest.GoalProgress
                    .FirstOrDefault(g => g.GoalName == goal.m_goalName);
                if (gInstance is null) {
                    Logger.Error("Player {0} has quest {1} but is missing goal {2}.",
                        Logger.Args(playerCharacter.CharId, relevantQuest.QuestName, goal.m_goalName));

                    continue;
                }

                if (relevantQuest.IsGoalActive(goal.m_goalName) && goal.m_personaName == myName) {
                    ShowQuestGoalCompletionDialog(playerActor, questTemplate, goal, relevantQuest.ID, gInstance.ID);

                    return;
                }
            }
        }

        // If no active persona goal, check if this NPC gives quests.
        if (_givesQuests.Count > 0) {
            var quest = _givesQuests.First();

            if (!playerCharacter.HasQuest(quest.m_questName) && !playerCharacter.HasCompletedQuest(quest.m_questName)) {
                ShowQuestInfoDialog(playerActor, quest);
                SendQuestOfferDialog(playerActor, quest);
                SendQuestOfferCacheOption(playerActor, quest);

                return;
            }
            else if (playerCharacter.HasQuest(quest.m_questName) && !playerCharacter.HasCompletedQuest(quest.m_questName)) {
                ShowQuestUnderwayDialog(playerActor, quest);

                return;
            }
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

    private void ShowQuestGoalCompletionDialog(IActorRef playerActor,
                                               QuestTemplate quest,
                                               GoalTemplate goal,
                                               ulong questId,
                                               ulong goalId) {
        if (quest.m_dialogList is not ActorDialogList dialogList) {
            Logger.Error("Quest {0} has no dialog list.",
                Logger.Args(quest.m_questName));

            return;
        }

        var completeDialogList = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Completion");
        if (completeDialogList is null) {
            Logger.Error("Quest {0} has no 'Completion' dialog entry.",
                Logger.Args(quest.m_questName));

            return;
        }

        // Serialize and send the dialog to the player.
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(completeDialogList, 16, out var serializedData)) {
            Logger.Error("Failed to serialize 'Completion' dialog for quest {0}.",
                Logger.Args(quest.m_questName));

            return;
        }

        // Send a message informing the client that they are interacting with an NPC
        // that will complete a quest goal.
        var goalCompleteMsg = new WIZARD_12_PROTOCOL.MSG_INTERACTCOMPLETEGOAL {
            MobileID = new GID(Entity.ActiveGameObject.m_nMobileID),
            QuestID = questId,
            GoalID = goalId,
        };

        playerActor.Tell(goalCompleteMsg);

        // Finally, send the dialog.
        var dialogMsg = new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = new GID(Entity.ActiveGameObject.m_nMobileID),
            QuestID = questId,
            GoalID = goalId,
            CompletionType = "Completion",
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