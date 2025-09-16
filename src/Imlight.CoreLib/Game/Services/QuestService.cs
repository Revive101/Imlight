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
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Services;

internal class QuestService(SessionActor sessionActor) : MessageService(sessionActor) {

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

        SendQuestStartingMessage(quest, questInstance);

        // Cache the quest template so we can reference it later if needed.
        if (!_cachedQuestTemplates.Contains(quest)) {
            _cachedQuestTemplates.Add(quest);
        }

        // Remove it from the cached offers now that it's accepted.
        _cachedQuestOffers.RemoveAll(q => q.m_questName == quest.m_questName);
    }

    private void SendQuestStartingMessage(QuestTemplate quest, QuestInstance questInstance) {
        var qMadLibs = GetMadLibForQuest(quest);
        if (!_goalSerializer.Serialize(qMadLibs, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for quest '{0}'",
                Logger.Args(quest.m_questName));

            return;
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
            AssociatedWorlds = "", // TODO:
            NoQuestHelper = quest.m_noQuestHelper ? (byte) 1 : (byte) 0,
            Mainline = quest.m_mainline ? (byte) 1 : (byte) 0,
            ReadyToTurnIn = 0,
            SkipQHAutoSelect = 0,
            PetOnlyQuest = quest.m_playAsYourPetNPC ? (byte) 1 : (byte) 0,
            ActivityType = 0, // ?
        };

        SendToSocket(qSendMsg);

        // Send the quest starting goals now.
        foreach (var gTemplate in quest.m_goals) {
            if (!quest.m_startGoals.Contains(gTemplate.m_goalName)) {
                continue;
            }

            SendGoalMessage(gTemplate, questInstance);
        }
    }

    private void SendQuestResumeMessage(QuestTemplate qTemplate, QuestInstance qInstance) {
        var qMadLibs = GetMadLibForQuest(qTemplate);
        if (!_goalSerializer.Serialize(qMadLibs, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for quest '{0}'",
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
            Rewards = "", // TODO:
            ClientTags = "",
            AssociatedWorlds = "", // TODO:
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

    private void SendGoalMessage(GoalTemplate gTemplate, QuestInstance qInstance) {
        var gInstance = qInstance.GoalProgress
            .FirstOrDefault(g => g.GoalName == gTemplate.m_goalName);
        if (gInstance == null) {
            // This should never happen, but log it just in case.
            Logger.Error("Quest '{0}' has no goal instance for template goal '{1}'.",
                Logger.Args(qInstance.QuestName, gTemplate.m_goalName));

            return;
        }

        // Serialize the madlib block for the goal.
        var madLibBlock = GetAppropriateMadlibBlockForGoal(gTemplate);
        if (!_goalSerializer.Serialize(madLibBlock, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for goal '{0}' in quest '{1}'",
                Logger.Args(gTemplate.m_goalName, qInstance.QuestName));

            return;
        }

        // Serialize the client tags, if present.
        var tagList = GetClientTagList(gTemplate.m_clientTags.ToArray() ?? []);
        if (!_goalSerializer.Serialize(tagList, 1, out var clientTagData)) {
            Logger.Error("Failed to serialize client tag data for goal '{0}' in quest '{1}'",
                Logger.Args(gTemplate.m_goalName, qInstance.QuestName));

            clientTagData = string.Empty;
        }

        SendToSocket(new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDGOAL {
            QuestID = qInstance.ID,
            GoalID = gInstance.ID,
            GoalNameID = gTemplate.m_goalNameID,
            GoalTitle = gTemplate.m_goalTitle,
            GoalLocation = gTemplate.m_locationName,
            GoalDestinationZone = gTemplate.m_destinationZone,
            GoalImage1 = gTemplate.m_displayImage1,
            GoalImage2 = gTemplate.m_displayImage2,
            PersonaName = "", // Probably useless (?)
            GoalType = (byte) gTemplate.m_goalType,
            GoalStatus = 0, // TODO: Determine status based on progress
            GoalCount = gInstance.CurrentProgress,

            UseTally = (byte) (gTemplate.m_tallyCounter is not null ? 1 : 0),
            GoalTotal = gTemplate.m_tallyCounter?.m_count ?? 0,
            TallyText = gTemplate.m_tallyCounter?.m_descriptor ?? "",
            SubscriberGoalTotal = 0, // TODO:
            SendType = 0, // ?
            GoalMadlibs = madLibData,
            ClientTags = clientTagData,
        });
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

        SendGoalMessage(goalTemplate, questInstance);
    }

    private void CompleteGoal(QuestInstance questInstance, GoalTemplate goalTemplate) {
        var wizard = GetActiveWizard();

        if (!wizard.CompleteQuestGoal(questInstance.QuestName, goalTemplate.m_goalName)) {
            Logger.Error("Failed to complete goal '{0}' for quest '{1}' for player '{2}'",
                Logger.Args(goalTemplate.m_goalName, questInstance.QuestName, wizard.CharId));

            return;
        }

        var gInstance = questInstance.GoalProgress
            .FirstOrDefault(g => g.GoalName == goalTemplate.m_goalName);
        if (gInstance == null || !gInstance.IsGoalCompleted()) {
            Logger.Error("Goal instance for goal '{0}' in quest '{1}' is not marked complete after completion.",
                Logger.Args(goalTemplate.m_goalName, questInstance.QuestName));

            return;
        }

        SendCompleteGoal(questInstance.ID, gInstance.ID);

        // Is there a next goal to start?
        var qTemplate = _cachedQuestTemplates
            .FirstOrDefault(q => q.m_questName == questInstance.QuestName);
        if (qTemplate == null) {
            Logger.Error("Failed to find quest template for quest '{0}' when completing goal '{1}'",
                Logger.Args(questInstance.QuestName, goalTemplate.m_goalName));

            return;
        }

        if (!DetermineNextGoals(qTemplate, questInstance, out var gTemplate)) {
            // No new goal. The player has completed the quest.
            return;
        }

        // Otherwise, we have new goals to start.
        foreach (var g in gTemplate) {
            StartGoal(questInstance, g);
        }
    }

    private void SendCompleteGoal(ulong questId, ulong goalId) {
        var gCompleteMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_COMPLETEGOAL {
            QuestID = questId,
            GoalID = goalId,
        };

        SendToSocket(gCompleteMsg);
    }

    private static bool DetermineNextGoals(QuestTemplate qTemplate,
                                          QuestInstance qinstance,
                                          out GoalTemplate[] gTemplate) {
        var goalLogic = qTemplate.m_goalLogic;
        if (goalLogic == null || goalLogic.Count == 0) {
            Logger.Error("Quest '{0}' has no goal logic defined.",
                Logger.Args(qTemplate.m_questName));

            gTemplate = null;

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
            gTemplate = null;

            return false;
        }

        // Return any goals we found to add
        if (allGoalsToAdd.Count > 0) {
            gTemplate = [.. allGoalsToAdd];

            return true;
        }

        // No applicable goal logic found.
        gTemplate = null;

        return false;
    }

    private static ClientTagList GetClientTagList(string[] tags)
        => new() {
            m_clientTags = [.. tags]
        };

    private static MadlibBlock GetMadLibForQuest(QuestTemplate quest)
        => new() {
            m_madlibs = [
                new MadlibArgT_string {
                    m_madlibToken = "NAME",
                    m_madlibArgument = quest.m_questTitle
                },
                new MadlibArgT_string {
                    m_madlibToken = "LEVEL",
                    m_madlibArgument = quest.m_questLevel.ToString()
                },
            ],
            m_blockToken = "QUEST"
        };

    private static MadlibBlock GetAppropriateMadlibBlockForGoal(GoalTemplate gTemplate)
        => gTemplate.m_goalType switch {
            GOAL_TYPE.GOAL_TYPE_WAYPOINT => GetMadlibBlockForWaypointGoal(gTemplate),
            GOAL_TYPE.GOAL_TYPE_PERSONA => GetMadlibBlockForPersonaGoal(gTemplate),
            _ => new MadlibBlock()
        };

    private static MadlibBlock GetMadlibBlockForWaypointGoal(GoalTemplate gTemplate)
        => new() {
            m_madlibs = [
                new MadlibArgT_string {
                    m_madlibToken = "NAME",
                    m_madlibArgument = gTemplate.m_goalTitle
                },
                new MadlibArgT_string {
                    m_madlibToken = "LOCATION",
                    m_madlibArgument = gTemplate.m_locationName
                },
                new MadlibArgT_string {
                    m_madlibToken = "TALLYTEXT",
                    m_madlibArgument = gTemplate.m_tallyCounter?.m_descriptor ?? string.Empty
                },
                new MadlibArgT_string {
                    m_madlibToken = "TALLYTEXT2",
                    m_madlibArgument = gTemplate.m_tallyCounter?.m_descriptor2 ?? string.Empty
                },
            ],
            m_blockToken = "GOAL"
        };

    private static MadlibBlock GetMadlibBlockForPersonaGoal(GoalTemplate gTemplate)
        => new() {
            m_madlibs = [
                new MadlibArgT_string {
                    m_madlibToken = "NAME",
                    m_madlibArgument = gTemplate.m_goalTitle
                },
                new MadlibArgT_string {
                    m_madlibToken = "LOCATION",
                    m_madlibArgument = gTemplate.m_locationName
                },
                new MadlibArgT_string {
                    m_madlibToken = "FIRSTNAME",
                    m_madlibArgument = "",
                },
                new MadlibArgT_string {
                    m_madlibToken = "LASTNAME",
                    m_madlibArgument = "",
                },
                new MadlibArgT_string {
                    m_madlibToken = "TITLE",
                    m_madlibArgument = "",
                },
                new MadlibArgT_string {
                    m_madlibToken = "FULLNAME",
                    m_madlibArgument = "NPCFormats_Goal_First_Last"
                }
            ],
            m_blockToken = "GOAL"
        };

}
