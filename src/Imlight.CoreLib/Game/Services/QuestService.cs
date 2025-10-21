/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * QUEST SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages in-game quest processing and player selection mechanisms
 * for administrative and interactive game functions.
 * 
 * USAGE EXAMPLE:
 * Internal service handling quest routing and player context
 * management within the game server session.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/14/2025
 */

using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Game.Results;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Services;

internal class QuestService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const float DEFAULT_KILL_COLLECT_CHANCE = 0.5f;

    private readonly List<QuestTemplate> _cachedQuestOffers = [];
    private readonly List<QuestTemplate> _cachedQuestTemplates = [];
    private readonly ObjectSerializer _goalSerializer = new(false);

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new QuestService(parentActor));

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();

        foreach (var qInstance in wizard.QuestBehavior.CurrentQuestInstances) {
            var qTemplate = QuestTemplateCollection.GetQuestByName(qInstance.QuestName);

            SendQuestResumeMessage(qTemplate, qInstance);

            // Cache the quest template so we can reference it later if needed.
            if (!_cachedQuestTemplates.Contains(qTemplate)) {
                _cachedQuestTemplates.Add(qTemplate);
            }
        }

        // Entering the zone may have triggered waypoint goals for quests.
        CheckForWaypointGoalZoneEntry(wizard);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_SENDQUESTOFFERCACHEOPTION))]
    private void ReceiveQuestOfferCache(CHARACTER_103_PROTOCOL.MSG_SENDQUESTOFFERCACHEOPTION message) {
        // An NPC component (InteractQuestComponent) has sent us a quest offer to cache.
        // We need to store this in the player's session context so that when they
        // accept the quest, we can process it.
        var quest = message.Quest;
        if (quest == null) {
            // Invalid quest data, ignore.
            return;
        }

        _cachedQuestOffers.Add(quest);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_COMPLETEPERSONAGOAL))]
    private void ReceiveCompletePersonaGoal(CHARACTER_103_PROTOCOL.MSG_COMPLETEPERSONAGOAL message) {
        var questId = message.QuestID;
        var goalId = message.GoalID;

        // Check if this player has this quest and goal active.
        var wizard = GetActiveWizard();
        var qInstance = wizard.QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q.ID == questId);
        if (qInstance == null) {
            Logger.Warning("Player '{0}' attempted to complete goal ID '{1}' for quest ID '{2}' but does not have that quest active.",
                Logger.Args(wizard.CharId, goalId, questId));
                
            return;
        }

        var gInstance = qInstance.GoalProgress
            .FirstOrDefault(g => g.ID == goalId);
        if (gInstance == null || !qInstance.IsGoalActive(gInstance.GoalName)) {
            Logger.Warning("Player '{0}' attempted to complete goal ID '{1}' for quest ID '{2}' but does not have that goal active.",
                Logger.Args(wizard.CharId, goalId, questId));
                
            return;
        }

        // Get the goal template for this goal.
        var qTemplate = _cachedQuestTemplates
            .FirstOrDefault(q => q.m_questName == qInstance.QuestName);
        if (qTemplate == null) {
            Logger.Error("Failed to find quest template for quest '{0}' when completing goal ID '{1}'",
                Logger.Args(qInstance.QuestName, goalId));
                
            return;
        }

        var gTemplate = qTemplate.m_goals
            .FirstOrDefault(g => g.m_goalName == gInstance.GoalName);
        if (gTemplate == null) {
            Logger.Error("Failed to find goal template for goal '{0}' in quest '{1}' when completing goal ID '{2}'",
                Logger.Args(gInstance.GoalName, qInstance.QuestName, goalId));
                
            return;
        }

        if (gTemplate.m_goalType != GOAL_TYPE.GOAL_TYPE_PERSONA) {
            Logger.Warning("Player '{0}' attempted to complete goal ID '{1}' for quest ID '{2}' but that goal is not a persona goal.",
                Logger.Args(wizard.CharId, goalId, questId));
                
            return;
        }

        CompleteGoal(qInstance, gTemplate);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_COMPLETESCAVENGEGOAL))]
    private void ReceiveCompleteScavengeGoal(CHARACTER_103_PROTOCOL.MSG_COMPLETESCAVENGEGOAL message) {
        var questId = message.QuestID;
        var goalId = message.GoalID;

        // Check if this player has this quest and goal active.
        var wizard = GetActiveWizard();
        var qInstance = wizard.QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q.ID == questId);
        if (qInstance == null) {
            Logger.Warning("Player '{0}' attempted to complete goal ID '{1}' for quest ID '{2}' but does not have that quest active.",
                Logger.Args(wizard.CharId, goalId, questId));
                
            return;
        }

        var gInstance = qInstance.GoalProgress
            .FirstOrDefault(g => g.ID == goalId);
        if (gInstance == null || !qInstance.IsGoalActive(gInstance.GoalName)) {
            Logger.Warning("Player '{0}' attempted to complete goal ID '{1}' for quest ID '{2}' but does not have that goal active.",
                Logger.Args(wizard.CharId, goalId, questId));
                
            return;
        }

        // Get the goal template for this goal.
        var qTemplate = _cachedQuestTemplates
            .FirstOrDefault(q => q.m_questName == qInstance.QuestName);
        if (qTemplate == null) {
            Logger.Error("Failed to find quest template for quest '{0}' when completing goal ID '{1}'",
                Logger.Args(qInstance.QuestName, goalId));
                
            return;
        }

        var gTemplate = qTemplate.m_goals
            .FirstOrDefault(g => g.m_goalName == gInstance.GoalName);
        if (gTemplate == null) {
            Logger.Error("Failed to find goal template for goal '{0}' in quest '{1}' when completing goal ID '{2}'",
                Logger.Args(gInstance.GoalName, qInstance.QuestName, goalId));
                
            return;
        }

        if (gTemplate.m_goalType != GOAL_TYPE.GOAL_TYPE_SCAVENGE) {
            Logger.Warning("Player '{0}' attempted to complete goal ID '{1}' for quest ID '{2}' but that goal is not a scavenge goal.",
                Logger.Args(wizard.CharId, goalId, questId));
                
            return;
        }

        CompleteGoal(qInstance, gTemplate);
    }

    [MessageHandler(typeof(QUEST_MESSAGES_52_PROTOCOL.MSG_ACCEPTQUEST))]
    private void ReceiveQuestAccept(QUEST_MESSAGES_52_PROTOCOL.MSG_ACCEPTQUEST message) {
        var account = GetActiveAccount();
        var wizard = GetActiveWizard();

        // Do we have this quest cached?
        var questName = message.QuestName;
        var quest = _cachedQuestOffers.Find(q => q.m_questName == questName);
        if (quest == null) {
            // There's not really a good reason why a player would send us this and we *don't*
            // have it cached. Log as suspicious activity.
            account.AddInfraction(
                infractionType: InfractionType.SuspiciousBehavior,
                reason: $"Player attempted to accept quest '{questName}' which was not offered."
            );

            Logger.Warning("Player '{0}' attempted to accept quest '{1}' which was not offered.",
                Logger.Args(wizard.CharId, questName));

            return;
        }

        // Otherwise, we're good to start the quest. Send them the send quest message and send goal message(s)
        // for any of the starting goals the quest has.
        var questInstance = new QuestInstance(quest, wizard.CharId);
        wizard.AddQuest(questInstance);

        if (!_cachedQuestTemplates.Contains(quest)) {
            _cachedQuestTemplates.Add(quest);
        }

        SendQuestStartingMessage(quest, questInstance);

        // Remove it from the cached offers now that it's accepted.
        _cachedQuestOffers.RemoveAll(q => q.m_questName == quest.m_questName);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATWIN))]
    private void ReceiveCombatVictory(COMBAT_106_PROTOCOL.MSG_COMBATWIN message) {
        var wizard = GetActiveWizard();

        foreach (var qInstance in wizard.QuestBehavior.CurrentQuestInstances) {
            var qTemplate = _cachedQuestTemplates.FirstOrDefault(q => q.m_questName == qInstance.QuestName);
            if (qTemplate == null) {
                Logger.Error("Failed to find quest template for quest '{0}' when processing combat victory.",
                    Logger.Args(qInstance.QuestName));

                continue;
            }

            var combatGoals = qTemplate.m_goals
                .Where(gTemplate => gTemplate.m_goalType == GOAL_TYPE.GOAL_TYPE_BOUNTY
                    || gTemplate.m_goalType == GOAL_TYPE.GOAL_TYPE_BOUNTYCOLLECT);

            if (!combatGoals.Any()) {
                continue;
            }

            foreach (var goal in combatGoals) {
                if (!qInstance.IsGoalActive(goal.m_goalName)) {
                    continue;
                }

                if (goal is not BountyGoalTemplate bountyGoal) {
                    Logger.Error("Combat goal '{0}' in quest '{1}' is not a valid bounty goal template.",
                        Logger.Args(goal.m_goalName, qInstance.QuestName));

                    continue;
                }

                ProcessCombatGoal(wizard, qInstance, bountyGoal, message.MobAdjectives);
            }
        }
    }

    private void SendQuestStartingMessage(QuestTemplate quest, QuestInstance questInstance) {
        var qMadLibs = QuestMadlibs.GetMadLibForQuest(quest);
        if (!_goalSerializer.Serialize(qMadLibs, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for quest '{0}'",
                Logger.Args(quest.m_questName));

            return;
        }

        // TODO: Replace with actual associated worlds from quest data.
        // For now, we'll just use "WizardCity" as a placeholder.
        var associatedWorlds = new AssociatedWorldsList {
            m_associatedWorlds = ["WizardCity"]
        };
        if (!_goalSerializer.Serialize(associatedWorlds, 1, out var associatedWorldData)) {
            Logger.Error("Failed to serialize associated world data for quest '{0}'",
                Logger.Args(quest.m_questName));

            associatedWorldData = string.Empty;
        }

        var qSendMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDQUEST {
            QuestID = questInstance.ID,
            QuestNameID = quest.m_questNameID,
            QuestType = 0, // ?
            QuestLevel = quest.m_questLevel,
            QuestTitle = quest.m_questTitle,
            QuestInfo = "", // ?
            New = 1,
            QuestMadlibs = madLibData,
            GoalData = "", // TODO:
            Rewards = "", // TODO:
            ClientTags = "",
            AssociatedWorlds = associatedWorldData,
            NoQuestHelper = quest.m_noQuestHelper ? (byte) 1 : (byte) 0,
            Mainline = quest.m_mainline ? (byte) 1 : (byte) 0,
            ReadyToTurnIn = 0,
            SkipQHAutoSelect = 0,
            PetOnlyQuest = quest.m_playAsYourPetNPC ? (byte) 1 : (byte) 0,
            ActivityType = 0, // ?
        };

        SendToSocket(qSendMsg);

        ShowQuestStartDialogue(quest);

        // Send the quest starting goals now.
        foreach (var gTemplate in quest.m_goals) {
            if (!quest.m_startGoals.Contains(gTemplate.m_goalName)) {
                continue;
            }

            StartGoal(questInstance, gTemplate);
        }

        // Init quest start results.
        var startResults = quest.m_startResults;
        ResultDispatcher.ExecuteResults(
            actorContext: Context,
            results: startResults,
            playerRef: SessionActor.ActorRef,
            playerObj: GetActiveGameObject(),
            questName: questInstance.QuestName
        );
    }

    private void SendQuestResumeMessage(QuestTemplate qTemplate, QuestInstance qInstance) {
        var qMadLibs = QuestMadlibs.GetMadLibForQuest(qTemplate);
        if (!_goalSerializer.Serialize(qMadLibs, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for quest '{0}'",
                Logger.Args(qTemplate.m_questName));

            return;
        }

        // TODO: Replace with actual associated worlds from quest data.
        // For now, we'll just use "WizardCity" as a placeholder.
        var associatedWorlds = new AssociatedWorldsList {
            m_associatedWorlds = ["WizardCity"]
        };
        if (!_goalSerializer.Serialize(associatedWorlds, 1, out var associatedWorldData)) {
            Logger.Error("Failed to serialize associated world data for quest '{0}'",
                Logger.Args(qTemplate.m_questName));

            associatedWorldData = string.Empty;
        }

        var rewards = GetQuestRewardsFromTemplate(qTemplate, null, SessionActor.ActorRef, GetActiveWizard());
        if (!_goalSerializer.Serialize(rewards, 1, out var serializedRewards)) {
            Logger.Error("Failed to serialize rewards for quest {0}.",
                Logger.Args(qTemplate.m_questName));

            return;
        }

        var qSendMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDQUEST {
            QuestID = qInstance.ID,
            QuestNameID = qTemplate.m_questNameID,
            QuestType = 0, // ?
            QuestLevel = qTemplate.m_questLevel,
            QuestTitle = qTemplate.m_questTitle,
            QuestInfo = "", // ?
            New = 0,
            QuestMadlibs = madLibData,
            GoalData = "", // TODO:
            Rewards = serializedRewards,
            ClientTags = "",
            AssociatedWorlds = associatedWorldData,
            NoQuestHelper = qTemplate.m_noQuestHelper ? (byte) 1 : (byte) 0,
            Mainline = qTemplate.m_mainline ? (byte) 1 : (byte) 0,
            ReadyToTurnIn = qInstance.IsReadyForTurnIn() ? (byte) 1 : (byte) 0,
            SkipQHAutoSelect = 0,
            PetOnlyQuest = qTemplate.m_playAsYourPetNPC ? (byte) 1 : (byte) 0,
            ActivityType = 0, // ?
        };

        SendToSocket(qSendMsg);

        // Send the quest's active goals now.
        foreach (var gTemplate in qTemplate.m_goals) {
            if (!qInstance.IsGoalActive(gTemplate.m_goalName)) {
                continue;
            }

            SendGoalMessage(gTemplate, qInstance);
        }
    }

    private void CheckForWaypointGoalZoneEntry(Wizard wizard) {
        var currentZone = wizard.Zone;

        foreach (var quest in _cachedQuestTemplates) {
            // Check to see if the quest has any waypoint goals that trigger on zone entry.
            var waypointGoals = quest.m_goals
                .Where(gTemplate => gTemplate.m_goalType == GOAL_TYPE.GOAL_TYPE_WAYPOINT)
                .Where(wTemplate => wTemplate is WaypointGoalTemplate wGoalTemplate
                    && wGoalTemplate.m_zoneEntry);

            if (!waypointGoals.Any()) {
                // No waypoint goals for this quest.
                continue;
            }

            // Check to see if the player has this goal active.
            var qInstance = wizard.QuestBehavior.CurrentQuestInstances
                .FirstOrDefault(q => q.QuestName == quest.m_questName);
            if (qInstance == null) {
                // Player doesn't have this quest.
                continue;
            }

            var activeWaypointGoals = waypointGoals
                .Where(gTemplate => qInstance.IsGoalActive(gTemplate.m_goalName));

            // Mark each of the goals complete if the player has entered the zone.
            foreach (var gTemplate in activeWaypointGoals) {
                if (gTemplate is not WaypointGoalTemplate waypointGoal) {
                    continue;
                }
                if (waypointGoal.m_destinationZone != currentZone) {
                    // Player is not in the correct zone for this goal.
                    continue;
                }

                CompleteGoal(qInstance, gTemplate);
            }
        }
    }

    private void StartGoal(QuestInstance questInstance, GoalTemplate goalTemplate) {
        var wizard = GetActiveWizard();

        if (!wizard.StartQuestGoal(questInstance.QuestName, goalTemplate.m_goalName)) {
            Logger.Error("Failed to start goal '{0}' for quest '{1}' for player '{2}'",
                Logger.Args(goalTemplate.m_goalName, questInstance.QuestName, wizard.CharId));

            return;
        }

        SendGoalMessage(goalTemplate, questInstance, 1);

        // Init goal activation results.
        var activationResults = goalTemplate.m_activateResults;
        ResultDispatcher.ExecuteResults(
            actorContext: Context,
            results: activationResults,
            playerRef: SessionActor.ActorRef,
            playerObj: GetActiveGameObject(),
            questName: questInstance.QuestName,
            goalName: goalTemplate.m_goalName
        );
    }

    private void CompleteGoal(QuestInstance questInstance, GoalTemplate goalTemplate) {
        var wizard = GetActiveWizard();

        if (!wizard.CompleteQuestGoal(questInstance.QuestName, goalTemplate.m_goalName)) {
            Logger.Error("Failed to complete goal '{0}' for quest '{1}' for player '{2}'",
                Logger.Args(goalTemplate.m_goalName, questInstance.QuestName, wizard.CharId));
            return;
        }

        var gInstance = questInstance.GoalProgress.FirstOrDefault(g => g.GoalName == goalTemplate.m_goalName);
        if (gInstance == null || !gInstance.IsGoalCompleted()) {
            Logger.Error("Goal instance for goal '{0}' in quest '{1}' is not marked complete after completion.",
                Logger.Args(goalTemplate.m_goalName, questInstance.QuestName));
            return;
        }

        SendCompleteGoal(questInstance.ID, gInstance.ID);
        ShowGoalCompletionDialogue(goalTemplate, questInstance.ID, gInstance.ID);

        ResultDispatcher.ExecuteResults(
            actorContext: Context,
            results: goalTemplate.m_completeResults,
            playerRef: SessionActor.ActorRef,
            playerObj: GetActiveGameObject(),
            questName: questInstance.QuestName,
            goalName: goalTemplate.m_goalName
        );

        var qTemplate = _cachedQuestTemplates.FirstOrDefault(q => q.m_questName == questInstance.QuestName);
        if (qTemplate == null) {
            Logger.Error("Failed to find quest template for quest '{0}' when completing goal '{1}'",
                Logger.Args(questInstance.QuestName, goalTemplate.m_goalName));
            return;
        }

        if (!DetermineNextGoals(qTemplate, questInstance, out var gTemplates)) {
            CompleteQuest(questInstance);
            
            return;
        }

        foreach (var g in gTemplates) {
            StartGoal(questInstance, g);
        }
    }

    private void CompleteQuest(QuestInstance questInstance) {
        var wizard = GetActiveWizard();

        if (!wizard.CompleteQuest(questInstance.QuestName)) {
            Logger.Error("Failed to complete quest '{0}' for player '{1}'",
                Logger.Args(questInstance.QuestName, wizard.CharId));
            return;
        }

        SendCompleteQuest(questInstance.ID);

        var qTemplate = _cachedQuestTemplates.FirstOrDefault(q => q.m_questName == questInstance.QuestName);
        if (qTemplate == null) {
            Logger.Error("Failed to find quest template for quest '{0}' when completing quest",
                Logger.Args(questInstance.QuestName));
            return;
        }

        ShowQuestCompletionDialogue(qTemplate);

        ResultDispatcher.ExecuteResults(
            actorContext: Context,
            results: qTemplate.m_endResults,
            playerRef: SessionActor.ActorRef,
            playerObj: GetActiveGameObject(),
            questName: questInstance.QuestName
        );
    }

    private void SendGoalMessage(GoalTemplate gTemplate, QuestInstance qInstance, byte sendType = 0) {
        var gInstance = qInstance.GoalProgress
            .FirstOrDefault(g => g.GoalName == gTemplate.m_goalName);
        if (gInstance == null) {
            // This should never happen, but log it just in case.
            Logger.Error("Quest '{0}' has no goal instance for template goal '{1}'.",
                Logger.Args(qInstance.QuestName, gTemplate.m_goalName));

            return;
        }

        // Serialize the madlib block for the goal.
        var madLibBlock = QuestMadlibs.GetAppropriateMadlibBlockForGoal(gTemplate, gInstance);
        if (!_goalSerializer.Serialize(madLibBlock, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for goal '{0}' in quest '{1}'",
                Logger.Args(gTemplate.m_goalName, qInstance.QuestName));

            return;
        }

        // Serialize the client tags, if present.
        var tagList = GetClientTagList(gTemplate.m_clientTags?.ToArray() ?? []);
        var newSerializer = new ObjectSerializer(false);
        if (!newSerializer.Serialize(tagList, 1, out var clientTagData)) {
            Logger.Error("Failed to serialize client tag data for goal '{0}' in quest '{1}'",
                Logger.Args(gTemplate.m_goalName, qInstance.QuestName));

            clientTagData = string.Empty;
        }

        var patronIcon = GetPatronIconFromGoal(gTemplate);

        var packet = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDGOAL {
            QuestID = qInstance.ID,
            GoalID = gInstance.ID,
            GoalNameID = gTemplate.m_goalNameID,
            GoalTitle = gTemplate.m_goalTitle,
            GoalLocation = gTemplate.m_locationName,
            GoalDestinationZone = sendType == 2 ? "" : gTemplate.m_destinationZone,
            GoalImage1 = gTemplate.m_displayImage1,
            GoalImage2 = gTemplate.m_displayImage2,
            PersonaName = "", // Probably useless (?)
            PatronIcon = patronIcon,
            GoalType = (byte) gTemplate.m_goalType,
            GoalStatus = 0, // TODO: Determine status based on progress
            GoalCount = gInstance.CurrentProgress,

            UseTally = (byte) (gTemplate.m_tallyCounter is not null ? 1 : 0),
            GoalTotal = gTemplate.m_tallyCounter?.m_count ?? 0,
            TallyText = gTemplate.m_tallyCounter?.m_descriptor ?? "",
            SubscriberGoalTotal = gTemplate.m_tallyCounter?.m_count ?? 0,
            SendType = sendType,
            GoalMadlibs = madLibData,
            ClientTags = clientTagData,
            NoQuestHelper = gTemplate.m_noQuestHelper ? (byte) 1 : (byte) 0
        };
        SendToSocket(packet);
    }

    private void SendCompleteGoal(ulong questId, ulong goalId) {
        var gCompleteMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_COMPLETEGOAL {
            QuestID = questId,
            GoalID = goalId,
        };

        SendToSocket(gCompleteMsg);
    }

    private void SendCompleteQuest(ulong questId) {
        var qCompleteMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_COMPLETEQUEST {
            QuestID = questId,
        };
        var qRemoveMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_REMOVEQUEST {
            QuestID = questId,
        };

        SendToSocket(qCompleteMsg);
        SendToSocket(qRemoveMsg);
    }

    private void ProcessCombatGoal(Wizard wizard,
                                   QuestInstance qInstance,
                                   BountyGoalTemplate goalTemplate,
                                   string[] defeatedMobAdjectives) {
        var shouldIncrement = goalTemplate.m_goalType == GOAL_TYPE.GOAL_TYPE_BOUNTY;
        var goalMobAdjectives = goalTemplate.m_npcAdjectives ?? [];

        // Check to see if the defeated mob matches the goal's NPC adjectives.
        if (!defeatedMobAdjectives.Any(adjective => goalMobAdjectives.Contains(adjective))) {
            return;
        }

        // If there were, determine how many of the adjectives matched.
        // We'll increment by that amount.
        var matchCount = defeatedMobAdjectives
            .Where(adjective => goalMobAdjectives.Contains(adjective))
            .Count();

        if (goalTemplate.m_goalType == GOAL_TYPE.GOAL_TYPE_BOUNTYCOLLECT) {
            var chance = goalTemplate.m_tallyCounter?.m_percentChance ?? DEFAULT_KILL_COLLECT_CHANCE;
            shouldIncrement = new Random().NextDouble() <= chance;
        }

        if (shouldIncrement) {
            for (int i = 0; i < matchCount; i++) {
                wizard.IncrementQuestGoal(qInstance.QuestName, goalTemplate.m_goalName);
            }

            var goalMax = goalTemplate.m_tallyCounter?.m_count ?? 0;
            var gInstance = qInstance.GoalProgress.FirstOrDefault(g => g.GoalName == goalTemplate.m_goalName);

            if (gInstance != null && gInstance.CurrentProgress >= goalMax) {
                CompleteGoal(qInstance, goalTemplate);

                return;
            }

            SendGoalMessage(goalTemplate, qInstance, 2);
        }
    }

    private string GetPatronIconFromGoal(GoalTemplate gTemplate) {
        var qTemplate = _cachedQuestTemplates
            .FirstOrDefault(q => q.m_goals.Contains(gTemplate));
        if (qTemplate == null) {
            return "";
        }

        // Check the quest dialog for the "Prep" section.
        // The patron will be whomever gave the player the quest.
        var dialogList = qTemplate.m_dialogList as ActorDialogList;
        var prepDialogList = dialogList?.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");

        return prepDialogList?.m_dialogEntries.FirstOrDefault()?.m_picture ?? "";
    }

    private static bool DetermineNextGoals(QuestTemplate qTemplate,
                                          QuestInstance qinstance,
                                          out GoalTemplate[] newGoalTemplates) {
        var goalLogic = qTemplate.m_goalLogic;
        if (goalLogic == null || goalLogic.Count == 0) {
            Logger.Error("Quest '{0}' has no goal logic defined.",
                Logger.Args(qTemplate.m_questName));

            newGoalTemplates = null;

            return false;
        }

        var allGoalsToAdd = new List<GoalTemplate>();
        var questShouldComplete = false;

        foreach (var gLogic in goalLogic) {
            // Validate logic entry has prerequisites.
            if ((gLogic.m_goalsAND == null || gLogic.m_goalsAND.Count == 0) &&
                (gLogic.m_goalsOR == null || gLogic.m_goalsOR.Count == 0)) {
                Logger.Error("Quest '{0}' has goal logic entry with no AND or OR prerequisites, skipping.",
                    Logger.Args(qTemplate.m_questName));

                continue;
            }

            // Check AND prerequisites - all must be complete.
            bool andPrereqsMet = true;
            if (gLogic.m_goalsAND != null) {
                foreach (var goalName in gLogic.m_goalsAND) {
                    if (!qinstance.IsGoalCompleted(goalName)) {
                        andPrereqsMet = false;

                        break;
                    }
                }
            }

            if (!andPrereqsMet) {
                continue;
            }

            // Check OR prerequisites - required count must be complete.
            // Skip if no OR goals are specified.
            if (gLogic.m_goalsOR != null && gLogic.m_goalsOR.Count > 0) {
                int completedORCount = 0;
                foreach (var goalName in gLogic.m_goalsOR) {
                    if (qinstance.IsGoalCompleted(goalName)) {
                        completedORCount++;
                    }
                }

                if (completedORCount < gLogic.m_requiredORCount) {
                    continue;
                }
            }

            // Prerequisites met - validate logic entry action.
            bool hasGoalsToAdd = gLogic.m_goalsToAdd != null && gLogic.m_goalsToAdd.Count > 0;

            if (!hasGoalsToAdd && !gLogic.m_completeQuest) {
                Logger.Warning("Quest '{0}' has goal logic entry that neither adds goals nor completes quest.",
                    Logger.Args(qTemplate.m_questName));

                continue;
            }

            if (hasGoalsToAdd && gLogic.m_completeQuest) {
                Logger.Error("Quest '{0}' has goal logic entry that both adds goals AND completes quest - invalid configuration.",
                    Logger.Args(qTemplate.m_questName));

                continue;
            }

            // Handle quest completion
            if (gLogic.m_completeQuest) {
                questShouldComplete = true;

                continue;
            }

            // Get goals to add that aren't already active or complete.
            foreach (var goalName in gLogic.m_goalsToAdd) {
                var goalTemplate = qTemplate.m_goals.FirstOrDefault(g => g.m_goalName == goalName);
                if (goalTemplate == null) {
                    Logger.Warning("Goal '{0}' referenced in quest logic but not found in quest '{1}'",
                        Logger.Args(goalName, qTemplate.m_questName));
                    continue;
                }

                var goalInstance = qinstance.GoalProgress.FirstOrDefault(g => g.GoalName == goalName);
                if (goalInstance != null && (goalInstance.DoesPlayerHaveGoal() || goalInstance.IsGoalCompleted())) {
                    continue;
                }

                // Avoid duplicate goals from multiple logic entries.
                if (!allGoalsToAdd.Any(g => g.m_goalName == goalName)) {
                    allGoalsToAdd.Add(goalTemplate);
                }
            }
        }

        // Quest completion takes precedence.
        if (questShouldComplete) {
            newGoalTemplates = null;

            return false;
        }

        // Return any goals we found to add
        if (allGoalsToAdd.Count > 0) {
            newGoalTemplates = [.. allGoalsToAdd];

            return true;
        }

        // Check if there are still active goals - if so, continue quest.
        var hasActiveGoals = qinstance.GoalProgress
            .Any(g => g.DoesPlayerHaveGoal() && !g.IsGoalCompleted());

        if (hasActiveGoals) {
            // Quest should continue - there are still active goals.
            newGoalTemplates = [];

            return true;
        }

        // No active goals and no new goals to add - quest should complete.
        newGoalTemplates = [];

        return false;
    }

    private static ClientTagList GetClientTagList(string[] tags)
        => new() {
            m_clientTags = [.. tags]
        };

    private static LootInfoList GetQuestRewardsFromTemplate(QuestTemplate qTemplate,
                                                            CoreObject playerObj,
                                                            IActorRef playerActor,
                                                            Wizard playerWizard) {
        // Quest rewards are listed in drop tables by "ResDropTable" in the completion results.
        var dropTableResults = qTemplate.m_endResults
            .m_results
            .Where(x => x is ResDropTable);

        var dropTableNames = dropTableResults
            .Select(x => (x as ResDropTable).m_tableName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        // "Roll" the drop tables to get the actual items.
        var rollResult = DropTableRoller.Roll(dropTableNames, playerActor, playerObj, playerWizard);

        // Convert the result into something we can send over the network.
        var convertedResults = DropTableConverter.ToLootInfoList(rollResult);

        return convertedResults;
    }

    private void ShowQuestStartDialogue(QuestTemplate questTemplate)
         => ShowDialogue(questTemplate.m_dialogList, "Start", "QuestStart");

    private void ShowGoalCompletionDialogue(GoalTemplate goalTemplate, ulong questId, ulong goalId)
        => ShowDialogue(goalTemplate.m_dialogList, "Completion", "Completion", questId, goalId);

    private void ShowQuestCompletionDialogue(QuestTemplate questTemplate)
        => ShowDialogue(questTemplate.m_dialogList, "Complete", "QuestComplete");

    private void ShowDialogue(ActorDialogListBase dialogListBase, string dialogTag, string completionType, ulong questId = 0, ulong goalId = 0) {
        if (dialogListBase is not ActorDialogList dialogList) {
            return;
        }

        var dialog = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == dialogTag);
        if (dialog != null) {
            SendActorDialog(dialog, completionType, questId, goalId);
        }
    }

    private void SendActorDialog(ActorDialog dialogEntry, string completionType, ulong questId = 0, ulong goalId = 0) {
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(dialogEntry, 16, out var serializedData)) {
            Logger.Error("Failed to serialize '{0}' dialog.",
                Logger.Args(completionType));
            return;
        }

        var dialogMsg = new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = 0,
            QuestID = questId,
            GoalID = goalId,
            CompletionType = completionType,
            ActorDialog = serializedData,
            Persona = "",
            PersonaName = "",
            PersonaIcon = "",
        };

        SendToSocket(dialogMsg);
    }

}
