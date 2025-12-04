/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Results;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal sealed class TutorialService(SessionActor sessionActor) : MessageService(sessionActor), IWithTimers {

    private const string TUTORIAL_QUEST_NAME = "WC-TUT-C05-001";
    private const uint TUTORIAL_NAME_STRING_ID = 600062081;
    private const string TUTORIAL_EXTERIOR_ZONE_NAME = "WizardCity/Tutorial_Exterior";
    private const string TUTORIAL_INTERIOR_ZONE_NAME = "WizardCity/Tutorial_Interior";

    private readonly ObjectSerializer _serializer = new(
        Versionable: false,
        Behaviors: SerializerFlags.None
    );
    private readonly TutorialInfo _tutorialInfo = new() {
        m_tutorialNameID = TUTORIAL_NAME_STRING_ID,
        m_tutorialStage = 0,
    };

    public ITimerScheduler Timers { get; set; }

    internal static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TutorialService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ATTACH))]
    private void ReceivePostAttach(GAME_5_PROTOCOL.MSG_ATTACH msg) {
        if (!_serializer.Serialize(_tutorialInfo,
                                   PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit,
                                   out var tutorialInfoBuffer)) {
            Logger.Error("Failed to serialize tutorial info");

            return;
        }

        var tutorialMsg = new GAME_5_PROTOCOL.MSG_TUTORIALS() {
            GlobalID = 1,
            Remove = 0,
            TutorialInfo = tutorialInfoBuffer
        };
        //SendToSocket(tutorialMsg);
        //SendToSocket(tutorialMsg);

        // We don't want to give the player the tutorial quest. The client's lua script handles that.
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_SERVERTUTORIALCOMMAND))]
    private void ReceiveServerTutorialCommand(GAME_5_PROTOCOL.MSG_SERVERTUTORIALCOMMAND msg) {
        // The tutorial is handled through a lua script on the client side, which sends commands to the server
        // to force complete certain quests/goals. We need to respond to those commands here.

        // For starters, we only want to allow tutorial commands if they are in the tutorial area 
        // and have not completed the tutorial quest yet.
        var wizard = GetActiveWizard();
        if (wizard.Zone is not (TUTORIAL_EXTERIOR_ZONE_NAME or TUTORIAL_INTERIOR_ZONE_NAME)) {
            return;
        }
        if (wizard.HasCompletedQuest(TUTORIAL_QUEST_NAME)) {
            return;
        }

        var playerObj = GetActiveGameObject();

        // Dispatch the command based off type.
        // Let GoalToComplete take priority over everything else, because sometimes the client will use both
        // the QuestToAdd and GoalToComplete in the same message.
        var commandSuccess = false;
        if (msg.GoalToComplete != string.Empty) {
            commandSuccess = HandleCommandCompleteGoal(wizard, playerObj, msg.GoalToComplete);
        }
        else if (msg.QuestToAdd != string.Empty) {
            commandSuccess = HandleCommandAddQuest(wizard, msg.QuestToAdd);
        }
        else if (msg.QuestToRemove != string.Empty) {
            commandSuccess = HandleCommandRemoveQuest(wizard, msg.QuestToRemove);
        }
        else if (msg.EventToPost != string.Empty) {
            commandSuccess = HandleCommandPostEvent(msg.EventToPost);
        }
        else if (msg.Action != string.Empty) {
            // TODO: unsure of what an action is
        }

        if (!commandSuccess) {
            Logger.Error("Failed to process tutorial command:"
                + "QuestToAdd='{0}'"
                + "QuestToRemove='{1}'"
                + "GoalToComplete='{2}"
                + "EventToPost='{3}'"
                + "ActionToPerform='{4}'",
                Logger.Args(
                    msg.QuestToAdd,
                    msg.QuestToRemove,
                    msg.GoalToComplete,
                    msg.EventToPost,
                    msg.Action
                ));
        }
        else {
            Logger.Debug("Processed tutorial command successfully:"
                + "QuestToAdd='{0}',"
                + "QuestToRemove='{1}'"
                + "GoalToComplete='{2}'"
                + "EventToPost='{3}'"
                + "ActionToPerform='{4}'",
                Logger.Args(
                    msg.QuestToAdd,
                    msg.QuestToRemove,
                    msg.GoalToComplete,
                    msg.EventToPost,
                    msg.Action
                ));
        }

        return;
    }

    private static bool HandleCommandAddQuest(Wizard wizard, string questName) {
        var questTemplate = QuestTemplateCollection.GetQuestByName(questName);
        if (questTemplate == null) {
            Logger.Error("Tutorial quest template not found");

            return false;
        }

        // Create a new quest instance.
        var qInstance = new QuestInstance(questTemplate, wizard.CharId);
        var addSuccess = wizard.AddQuest(qInstance);

        return addSuccess;
    }

    private static bool HandleCommandRemoveQuest(Wizard wizard, string questName) {
        var removeSuccess = wizard.RemoveQuest(questName);

        return removeSuccess;
    }

    private bool HandleCommandCompleteGoal(Wizard wizard,
                                           CoreObject playerObj,
                                           string goalName) {
        // We need to find the quest that contains this goal.
        // We can search the player for quest/goal instances, as they do carry a name.
        var allWizardQuestInstances = wizard.QuestBehavior.CurrentQuestInstances;
        var goalInstance = allWizardQuestInstances
            .SelectMany(q => q.GoalProgress, (quest, goal) => new { quest, goal })
            .FirstOrDefault(x => x.goal.GoalName == goalName);

        if (goalInstance != null) {
            // We can remove it from the Wizard, but we need to post the completion events as well.
            var removeSuccess = wizard.CompleteQuestGoal(goalInstance.quest.QuestName, goalName);

            // We need the actual goal instance template to get the completion results.
            var questTemplate = QuestTemplateCollection.GetQuestByName(goalInstance.quest.QuestName);
            if (questTemplate == null) {
                Logger.Error("Tutorial quest template not found");

                return false;
            }

            // Find the goal template within the quest template.
            var goalTemplate = questTemplate.m_goals
                .FirstOrDefault(g => g.m_goalName == goalName);
            if (goalTemplate == null) {
                Logger.Error("Tutorial goal template not found");

                return false;
            }

            ResultDispatcher.ExecuteResults(
                actorContext: Context,
                results: goalTemplate.m_completeResults,
                playerRef: SessionActor.ActorRef,
                playerObj: playerObj,
                questName: goalInstance.quest.QuestName,
                goalName: goalInstance.goal.GoalName,
                zoneActor: SessionActor.GetZoneActor()
            );

            return removeSuccess;
        }

        return false;
    }

    private bool HandleCommandPostEvent(string eventName) {
        ZoneBroadcastNoPlayers(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
            EventName = eventName,
            PlayerActor = SessionActor.ActorRef
        });

        return true;
    }

}