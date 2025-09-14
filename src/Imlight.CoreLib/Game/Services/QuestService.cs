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
    private readonly ObjectSerializer _goalSerializer = new(false);

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new QuestService(parentActor));

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();

        foreach (var qInstance in wizard.QuestBehavior.CurrentQuestInstances) {
            var qTemplate = QuestTemplateCollection.GetQuestByName(qInstance.QuestName);

            SendQuestResumeMessage(qTemplate, qInstance);
            SendQuestResumeGoalMessages(qTemplate, qInstance);
        }
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

            return;
        }

        // Otherwise, we're good to start the quest. Send them the send quest message and send goal message(s)
        // for any of the starting goals the quest has.
        var questInstance = new QuestInstance(quest, wizard.CharId);

        // Find the starting goals and mark them as started.
        var startingGoals = quest.m_goals
            .Where(g => quest.m_startGoals.Contains(g.m_goalName))
            .ToArray();
        foreach (var gTemplate in startingGoals) {
            questInstance.StartGoal(gTemplate.m_goalName);
        }

        wizard.AddQuest(questInstance);

        SendQuestStartingMessage(quest, questInstance.ID);
        SendQuestStartingGoalMessages(quest, questInstance);
    }

    private void SendQuestStartingMessage(QuestTemplate quest, ulong uniqueQuestID) {
        var qMadLibs = GetMadLibForQuest(quest);
        if (!_goalSerializer.Serialize(qMadLibs, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for quest '{0}'",
                Logger.Args(quest.m_questName));

            return;
        }

        var qSendMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDQUEST {
            QuestID = uniqueQuestID,
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
    }

    private void SendQuestStartingGoalMessages(QuestTemplate quest, QuestInstance questInstance) {
        // Determine which goals are starting goals (i.e. have no prerequisites)
        var startingGoals = quest.m_goals
            .Where(g => quest.m_startGoals.Contains(g.m_goalName))
            .ToArray();

        // Send messages for each starting goal
        foreach (var gTemplate in startingGoals) {
            // Find the corresponding GoalInstance in the QuestInstance
            var goalInstance = questInstance.GoalProgress
                .FirstOrDefault(g => g.GoalName == gTemplate.m_goalTitle);

            var madLibBlock = GetMadlibBlockForGoal(gTemplate);
            if (!_goalSerializer.Serialize(madLibBlock, 1, out var madLibData)) {
                Logger.Error("Failed to serialize madlib data for goal '{0}' in quest '{1}'",
                    Logger.Args(gTemplate.m_goalTitle, quest.m_questName));

                continue;
            }

            var gSendMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDGOAL {
                QuestID = questInstance.ID,
                GoalID = goalInstance?.ID ?? 0,
                GoalNameID = gTemplate.m_goalNameID,
                GoalTitle = gTemplate.m_goalTitle,
                GoalLocation = gTemplate.m_locationName,
                GoalDestinationZone = gTemplate.m_destinationZone,
                GoalImage1 = gTemplate.m_displayImage1,
                GoalImage2 = gTemplate.m_displayImage2,
                PersonaName = "", // Probably useless (?)
                GoalType = (byte) gTemplate.m_goalType,
                GoalStatus = 0, // This is a starting goal! It cannot be complete yet.
                GoalCount = 0, // Starting at 0 progress.
                SubscriberGoalTotal = 0, // TODO:
                SendType = 0, // ?
                GoalMadlibs = madLibData,

                UseTally = (byte) (gTemplate.m_tallyCounter is not null ? 1 : 0),
                GoalTotal = gTemplate.m_tallyCounter?.m_count ?? 0,
                TallyText = gTemplate.m_tallyCounter?.m_descriptor ?? "",
            };

            SendToSocket(gSendMsg);
        }
    }

    private void SendQuestResumeGoalMessages(QuestTemplate quest, QuestInstance questInstance) {
        foreach (var gInstance in questInstance.GoalProgress) {
            // Skip this goal if the player doesn't have it yet.
            if (!gInstance.DoesPlayerHaveGoal()) {
                continue;
            }

            var gTemplate = quest.m_goals
                .FirstOrDefault(g => g.m_goalName == gInstance.GoalName);
            if (gTemplate == null) {
                // This should never happen, but log it just in case.
                Logger.Error("Quest '{0}' has goal instance '{1}' with no matching template.",
                    Logger.Args(quest.m_questName, gInstance.GoalName));

                continue;
            }

            var madLibBlock = GetMadlibBlockForGoal(gTemplate);
            if (!_goalSerializer.Serialize(madLibBlock, 1, out var madLibData)) {
                Logger.Error("Failed to serialize madlib data for goal '{0}' in quest '{1}'",
                    Logger.Args(gTemplate.m_goalName, quest.m_questName));

                continue;
            }

            var gSendMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDGOAL {
                QuestID = questInstance.ID,
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
                SubscriberGoalTotal = 0, // TODO:
                SendType = 0, // ?
                GoalMadlibs = madLibData,

                UseTally = (byte) (gTemplate.m_tallyCounter is not null ? 1 : 0),
                GoalTotal = gTemplate.m_tallyCounter?.m_count ?? 0,
                TallyText = gTemplate.m_tallyCounter?.m_descriptor ?? "",
            };

            SendToSocket(gSendMsg);
        }
    }

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

    private static MadlibBlock GetMadlibBlockForGoal(GoalTemplate gTemplate)
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

}
