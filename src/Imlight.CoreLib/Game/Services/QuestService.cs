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
 * Last Updated: 09/27/2026
 */

using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
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
    private const string QUEST_COMPLETED_ENTRY = "Complete";
    private const string GOAL_COMPLETE_EVENT_PREFIX = "GoalComplete_";
    private const string GOAL_COMPLETION_DIALOG_TAG = "Completion";
    private const string DIALOG_ENTRY_EVENT_COMPLETION = "ENTRY";

    private const float FIRST_RUN_XP_SCALE = 1f;
    private const float SECOND_RUN_XP_SCALE = 0.5f;
    private const float LATER_RUN_XP_SCALE = 0f;
    private static readonly TimeSpan INSTANCE_QUERY_TIMEOUT = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How a goal completes: on the player's own progress, because another player in the instance completed it, or
    /// as the instance's progress is replayed to a player who joins late.
    /// </summary>
    private enum GoalCompletion {
        Own,
        Shared,
        CatchUp,
    }

    private static readonly TimeSpan PENDING_GOAL_DIALOG_TIMEOUT = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PERSONA_TRANSITION_DELAY = TimeSpan.FromMilliseconds(1500);

    private readonly List<QuestTemplate> _cachedQuestOffers = [];
    private readonly List<QuestTemplate> _cachedQuestTemplates = [];
    private readonly ObjectSerializer _goalSerializer = new(false);

    /// <summary>
    /// A persona goal whose completion dialog is on the player's screen. Live completes a persona goal only
    /// when the client reports that dialog finished (MSG_COMPLETEPERSONA, or MSG_COMPLETEDIALOG), so the goal,
    /// its results (e.g. a teleport) and the next goals wait here. Nothing is written to the character before
    /// then: if the session ends or the player changes zone first, the goal is still active and talking to
    /// the NPC replays it.
    /// </summary>
    private sealed record PendingGoalCompletion(ulong QuestId, ulong GoalId,
        IActorRef TransitionTarget, IServerMessage TransitionMessage);

    private readonly List<PendingGoalCompletion> _pendingGoalCompletions = [];
    private bool _clientReportsPersonaCompletion;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new QuestService(parentActor));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PRELOGIN))]
    private void ReceiveSendQuests(ZONE_102_PROTOCOL.MSG_PRELOGIN message) {
        // A completion dialog does not survive a zone change. Its goal was never completed, so it is
        // resent below as active and the player can talk to the NPC again.
        DropPendingGoalCompletions("zone change");

        var wizard = GetActiveWizard();
        RemoveDungeonQuestsOutsideZone(wizard);

        foreach (var qInstance in wizard.QuestBehavior.CurrentQuestInstances) {
            var qTemplate = QuestTemplateCollection.GetQuestByName(qInstance.QuestName);

            SendQuestResumeMessage(qTemplate, qInstance);

            // Cache the quest template so we can reference it later if needed.
            if (!_cachedQuestTemplates.Contains(qTemplate)) {
                _cachedQuestTemplates.Add(qTemplate);
            }
        }
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();

        // Drop quest instance IDs that no longer have a backing instance before
        // any grant or waypoint logic runs on them.
        wizard.QuestBehavior.PruneStaleQuestIds();

        // Dungeon quests are force-added on entry (a quest marked by a ReqInZone requirement for this
        // zone). Grant BEFORE the waypoint check so a fresh quest's enter-the-dungeon goal completes on
        // this same attach.
        TryGrantDungeonQuests(wizard, reconcileWithInstance: true);

        // Entering the zone may have triggered waypoint goals for quests.
        CheckForWaypointGoalZoneEntry(wizard);

        // Exiting the previous zone may have triggered waypoint goals for quests.
        CheckForWaypointGoalZoneExit(wizard);
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

        // Live sends a persona goal's completion dialog first and completes the goal only when the client
        // reports the dialog closed (MSG_COMPLETEDIALOG). Show it now and finish the goal from there.
        if (FindDialogue(gTemplate.m_dialogList, GOAL_COMPLETION_DIALOG_TAG) is not null) {
            if (_pendingGoalCompletions.Any(p => p.QuestId == questId && p.GoalId == goalId)) {
                Logger.Debug("Player '{0}' asked again to complete goal ID '{1}' for quest ID '{2}' while its completion dialog is open; ignoring.",
                    Logger.Args(wizard.CharId, goalId, questId));

                return;
            }

            ShowGoalCompletionDialogue(gTemplate, questId, goalId);

            _pendingGoalCompletions.Add(new PendingGoalCompletion(questId, goalId,
                message.TransitionTarget, message.TransitionMessage));
            Timers.StartSingleTimer(
                PendingGoalTimerKey(questId, goalId),
                new CHARACTER_103_PROTOCOL.MSG_GOALDIALOGTIMEOUT { QuestID = questId, GoalID = goalId },
                PENDING_GOAL_DIALOG_TIMEOUT);

            return;
        }

        CompleteGoal(qInstance, gTemplate);
        StartPersonaTransition(message.TransitionTarget, message.TransitionMessage);
    }

    [MessageHandler(typeof(QUEST_MESSAGES_52_PROTOCOL.MSG_COMPLETEPERSONA))]
    private void ReceiveCompletePersona(QUEST_MESSAGES_52_PROTOCOL.MSG_COMPLETEPERSONA message) {
        // Live's client sends this when a persona goal's completion dialog ends, naming the quest and goal.
        _clientReportsPersonaCompletion = true;

        var pending = _pendingGoalCompletions
            .FirstOrDefault(p => p.QuestId == message.QuestID && p.GoalId == message.GoalID);
        if (pending is null) {
            return;
        }

        FinishPendingGoalCompletion(pending);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_COMPLETEDIALOG))]
    private void ReceiveCompleteDialog(WIZARD_12_PROTOCOL.MSG_COMPLETEDIALOG message) {
        // The client sends CompletionType "ENTRY" when it reaches an entry with a dialog event, and an
        // empty CompletionType once the dialog window closes. Only the close finishes a goal, and only
        // for a client that has not shown it reports persona completions itself.
        string completionType = message.CompletionType;
        if (string.Equals(completionType, DIALOG_ENTRY_EVENT_COMPLETION, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        if (_clientReportsPersonaCompletion || _pendingGoalCompletions.Count == 0) {
            return;
        }

        // Every dialog we send carries MobileID 0, so the close can't be matched by NPC. Dialogs close in
        // the order they were shown: the oldest pending goal is the one whose dialog just closed.
        FinishPendingGoalCompletion(_pendingGoalCompletions[0]);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_GOALDIALOGTIMEOUT))]
    private void ReceiveGoalDialogTimeout(CHARACTER_103_PROTOCOL.MSG_GOALDIALOGTIMEOUT message) {
        var pending = _pendingGoalCompletions
            .FirstOrDefault(p => p.QuestId == message.QuestID && p.GoalId == message.GoalID);
        if (pending is null) {
            return;
        }

        // A safety net only: a long voiced dialog can legitimately stay open for a minute.
        Logger.Warning("No dialog close reported for goal ID '{0}' of quest ID '{1}' after {2}; completing the goal anyway.",
            Logger.Args(message.GoalID, message.QuestID, PENDING_GOAL_DIALOG_TIMEOUT));

        FinishPendingGoalCompletion(pending);
    }

    private void FinishPendingGoalCompletion(PendingGoalCompletion pending) {
        _pendingGoalCompletions.Remove(pending);
        Timers.Cancel(PendingGoalTimerKey(pending.QuestId, pending.GoalId));

        // Re-check: the quest may have been dropped or the goal completed elsewhere while the dialog was up.
        var wizard = GetActiveWizard();
        var qInstance = wizard.QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q.ID == pending.QuestId);
        var gInstance = qInstance?.GoalProgress
            .FirstOrDefault(g => g.ID == pending.GoalId);
        if (gInstance == null || !qInstance.IsGoalActive(gInstance.GoalName)) {
            Logger.Warning("Goal ID '{0}' of quest ID '{1}' is no longer active when its completion dialog closed; nothing to complete.",
                Logger.Args(pending.GoalId, pending.QuestId));

            return;
        }

        var gTemplate = _cachedQuestTemplates
            .FirstOrDefault(q => q.m_questName == qInstance.QuestName)?.m_goals
            .FirstOrDefault(g => g.m_goalName == gInstance.GoalName);
        if (gTemplate == null) {
            Logger.Error("Failed to find goal template for goal '{0}' in quest '{1}' when its completion dialog closed.",
                Logger.Args(gInstance.GoalName, qInstance.QuestName));

            return;
        }

        // The dialog was already shown when the player talked to the NPC.
        CompleteGoal(qInstance, gTemplate, showCompletionDialogue: false);
        StartPersonaTransition(pending.TransitionTarget, pending.TransitionMessage);
    }

    private void DropPendingGoalCompletions(string reason) {
        foreach (var pending in _pendingGoalCompletions) {
            Timers.Cancel(PendingGoalTimerKey(pending.QuestId, pending.GoalId));

            Logger.Debug("Goal ID '{0}' of quest ID '{1}' left active: its completion dialog was interrupted by {2}.",
                Logger.Args(pending.GoalId, pending.QuestId, reason));
        }

        _pendingGoalCompletions.Clear();
    }

    private void StartPersonaTransition(IActorRef target, IServerMessage transitionMessage) {
        if (target is null || transitionMessage is null) {
            return;
        }

        // The NPC re-offers after the quest completion's results have had a moment to land.
        Context.System.Scheduler.ScheduleTellOnce(PERSONA_TRANSITION_DELAY, target, transitionMessage, Self);
    }

    private static string PendingGoalTimerKey(ulong questId, ulong goalId)
        => $"goal-dialog-{questId}-{goalId}";

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_COMPLETEUSAGEGOAL))]
    private void ReceiveCompleteScavengeGoal(CHARACTER_103_PROTOCOL.MSG_COMPLETEUSAGEGOAL message) {
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

        if (gTemplate.m_goalType != GOAL_TYPE.GOAL_TYPE_USAGE) {
            Logger.Warning("Player '{0}' attempted to complete goal ID '{1}' for quest ID '{2}' but that goal is not a usage goal.",
                Logger.Args(wizard.CharId, goalId, questId));

            return;
        }

        wizard.IncrementQuestGoal(qInstance.QuestName, gTemplate.m_goalName);

        var goalMax = gTemplate.m_tallyCounter?.m_count ?? 1;
        if (gInstance.CurrentProgress >= goalMax) {
            CompleteGoal(qInstance, gTemplate, showCompletionDialogue: !message.SuppressCompletionDialog);

            return;
        }

        SendGoalMessage(gTemplate, qInstance, 2);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_COMPLETEPROXIMITYGOAL))]
    private void ReceiveCompleteProximityGoal(ZONE_102_PROTOCOL.MSG_COMPLETEPROXIMITYGOAL message) {
        // A volume component matched one of this player's active proximity goals to the
        // volume they walked into; complete the goal it named.
        var wizard = GetActiveWizard();
        var qInstance = wizard.QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q.ID == message.QuestID);
        if (qInstance == null) {
            Logger.Warning("Player '{0}' attempted to complete proximity goal ID '{1}' for quest ID '{2}' but does not have that quest active.",
                Logger.Args(wizard.CharId, message.GoalID, message.QuestID));

            return;
        }

        var gInstance = qInstance.GoalProgress
            .FirstOrDefault(g => g.ID == message.GoalID);
        if (gInstance == null || !qInstance.IsGoalActive(gInstance.GoalName)) {
            Logger.Warning("Player '{0}' attempted to complete proximity goal ID '{1}' for quest ID '{2}' but does not have that goal active.",
                Logger.Args(wizard.CharId, message.GoalID, message.QuestID));

            return;
        }

        // Get the goal template for this goal.
        var qTemplate = _cachedQuestTemplates
            .FirstOrDefault(q => q.m_questName == qInstance.QuestName);
        if (qTemplate == null) {
            Logger.Error("Failed to find quest template for quest '{0}' when completing proximity goal ID '{1}'",
                Logger.Args(qInstance.QuestName, message.GoalID));

            return;
        }

        var gTemplate = qTemplate.m_goals
            .FirstOrDefault(g => g.m_goalName == gInstance.GoalName);
        if (gTemplate == null) {
            Logger.Error("Failed to find goal template for goal '{0}' in quest '{1}' when completing proximity goal ID '{2}'",
                Logger.Args(gInstance.GoalName, qInstance.QuestName, message.GoalID));

            return;
        }

        if (gTemplate.m_goalType != GOAL_TYPE.GOAL_TYPE_WAYPOINT) {
            Logger.Warning("Player '{0}' attempted to complete proximity goal ID '{1}' for quest ID '{2}' but that goal is not a waypoint goal.",
                Logger.Args(wizard.CharId, message.GoalID, message.QuestID));

            return;
        }

        CompleteGoal(qInstance, gTemplate);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEEVENTFORQUESTS))]
    private void ReceiveZoneEventForQuests(ZONE_102_PROTOCOL.MSG_ZONEEVENTFORQUESTS message) {
        // A zone event was posted: every active goal that lists it in m_genericEvents completes.
        if (string.IsNullOrEmpty(message.EventName)) {
            return;
        }

        var wizard = GetActiveWizard();
        foreach (var qInstance in wizard.QuestBehavior.CurrentQuestInstances.ToList()) {
            var qTemplate = _cachedQuestTemplates.FirstOrDefault(q => q.m_questName == qInstance.QuestName);
            if (qTemplate?.m_goals is null) {
                continue;
            }

            foreach (var gTemplate in qTemplate.m_goals) {
                if (gTemplate.m_genericEvents is null
                    || !gTemplate.m_genericEvents.Contains(message.EventName)
                    || !qInstance.IsGoalActive(gTemplate.m_goalName)) {
                    continue;
                }

                CompleteGoal(qInstance, gTemplate);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_INSTANCEGOALCOMPLETED))]
    private void ReceiveInstanceGoalCompleted(ZONE_102_PROTOCOL.MSG_INSTANCEGOALCOMPLETED message) {
        if (SessionActor.ActorRef.Equals(message.Origin)) {
            return;
        }

        var qInstance = GetActiveWizard().QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q.QuestName == message.QuestName);
        var gTemplate = _cachedQuestTemplates
            .FirstOrDefault(q => q.m_questName == message.QuestName)?.m_goals?
            .FirstOrDefault(g => g.m_goalName == message.GoalName);
        if (qInstance is null || gTemplate is null || !qInstance.IsGoalActive(gTemplate.m_goalName)) {
            return;
        }

        CompleteGoal(qInstance, gTemplate, completion: GoalCompletion.Shared);
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

        if (DungeonQuestIndex.IsDungeonQuest(quest.m_questName)) {
            Logger.Warning("Player '{0}' attempted to accept dungeon quest '{1}', which the instance grants.",
                Logger.Args(wizard.CharId, questName));

            return;
        }

        // Otherwise, we're good to start the quest. Send them the send quest message and send goal message(s)
        // for any of the starting goals the quest has.
        StartQuest(quest, wizard);

        // Remove it from the cached offers now that it's accepted.
        _cachedQuestOffers.RemoveAll(q => q.m_questName == quest.m_questName);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_QUESTCOMMAND))]
    private void ReceiveQuestCommand(CHARACTER_103_PROTOCOL.MSG_QUESTCOMMAND message) {
        var wizard = GetActiveWizard();
        var questName = message.QuestName?.Trim() ?? string.Empty;

        switch (message.Action) {
            case "list":
                CommandListQuests(wizard);
                return;
            case "grant":
            case "forcegrant":
                CommandGrantQuest(wizard, questName, message.Action == "forcegrant");
                return;
            case "remove":
                CommandRemoveQuest(wizard, questName);
                return;
        }

        // The remaining actions act on an active quest. The server does not learn which quest the client
        // has tracked, so an empty name means the wizard's only active quest.
        var qInstance = ResolveCommandQuest(wizard, questName);
        if (qInstance is null) {
            return;
        }

        var qTemplate = GetQuestTemplate(qInstance.QuestName);
        if (qTemplate?.m_goals is null) {
            CommandReply($"Quest '{qInstance.QuestName}' has no template.");
            return;
        }

        switch (message.Action) {
            case "completegoal":
                CommandCompleteGoal(qInstance, qTemplate);
                break;
            case "complete":
                CommandCompleteQuest(qInstance, qTemplate);
                break;
            case "info":
                CommandQuestInfo(qInstance, qTemplate);
                break;
            default:
                CommandReply($"Unknown quest action '{message.Action}'.");
                break;
        }
    }

    private void CommandReply(string text)
        => SendToSocket(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE { Message = text, Modal = 0 });

    private QuestTemplate GetQuestTemplate(string questName) {
        var template = _cachedQuestTemplates.FirstOrDefault(q => q.m_questName == questName)
            ?? QuestTemplateCollection.GetQuestByName(questName);
        if (template is not null && !_cachedQuestTemplates.Contains(template)) {
            _cachedQuestTemplates.Add(template);
        }

        return template;
    }

    private QuestInstance ResolveCommandQuest(Wizard wizard, string questName) {
        var active = wizard.QuestBehavior.CurrentQuestInstances.Where(q => q is not null).ToList();
        if (string.IsNullOrEmpty(questName)) {
            if (active.Count == 1) {
                return active[0];
            }

            CommandReply(active.Count == 0
                ? "You have no active quests."
                : "You have several active quests: give a quest name (see '.quest list').");
            return null;
        }

        var found = active.FirstOrDefault(q => q.QuestName.Equals(questName, StringComparison.OrdinalIgnoreCase));
        if (found is null) {
            CommandReply(QuestTemplateCollection.GetQuestByName(questName) is null
                ? $"Quest '{questName}' does not exist."
                : $"You do not have the quest '{questName}' active.");
        }

        return found;
    }

    private static IEnumerable<GoalTemplate> ActiveGoals(QuestInstance qInstance, QuestTemplate qTemplate)
        => qTemplate.m_goals.Where(g => qInstance.IsGoalActive(g.m_goalName));

    private void CommandListQuests(Wizard wizard) {
        var active = wizard.QuestBehavior.CurrentQuestInstances.Where(q => q is not null).ToList();
        if (active.Count == 0) {
            CommandReply("You have no active quests.");
            return;
        }

        foreach (var qInstance in active) {
            var qTemplate = GetQuestTemplate(qInstance.QuestName);
            var goals = qTemplate?.m_goals is null
                ? []
                : ActiveGoals(qInstance, qTemplate).Select(g => g.m_goalName).ToList();
            CommandReply($"{qInstance.QuestName}: {(goals.Count == 0 ? "no active goal" : string.Join(", ", goals))}");
        }
    }

    private void CommandQuestInfo(QuestInstance qInstance, QuestTemplate qTemplate) {
        CommandReply($"{qInstance.QuestName} (level {qTemplate.m_questLevel}):");
        foreach (var gTemplate in qTemplate.m_goals) {
            var gInstance = qInstance.GoalProgress.FirstOrDefault(g => g.GoalName == gTemplate.m_goalName);
            string state;
            if (gInstance is null || gInstance.CurrentProgress < 0) {
                state = "not started";
            }
            else if (gInstance.IsGoalCompleted()) {
                state = "done";
            }
            else {
                var total = gTemplate.m_tallyCounter?.m_count ?? 0;
                state = total > 0 ? $"active {gInstance.CurrentProgress}/{total}" : "active";
            }

            CommandReply($"  {gTemplate.m_goalName} [{gTemplate.m_goalType}]: {state}");
        }
    }

    private void CommandCompleteGoal(QuestInstance qInstance, QuestTemplate qTemplate) {
        var goal = ActiveGoals(qInstance, qTemplate).FirstOrDefault();
        if (goal is null) {
            CommandReply($"Quest '{qInstance.QuestName}' has no active goal.");
            return;
        }

        CommandReply($"Completing goal '{goal.m_goalName}' of '{qInstance.QuestName}'.");
        CompleteGoal(qInstance, goal);
    }

    private void CommandCompleteQuest(QuestInstance qInstance, QuestTemplate qTemplate) {
        var wizard = GetActiveWizard();
        var questName = qInstance.QuestName;

        // Walk the goals the way a player would: each completion starts the next goals and, after the last
        // one, completes the quest with its end results. Bounded so a looping quest cannot hang the session.
        for (var step = 0; step < 256 && wizard.HasQuest(questName); step++) {
            var goal = ActiveGoals(qInstance, qTemplate).FirstOrDefault();
            if (goal is null) {
                break;
            }

            CompleteGoal(qInstance, goal, showCompletionDialogue: false);
        }

        if (wizard.HasQuest(questName)) {
            // Nothing left to complete (for example a goal whose requirements were not met): finish it.
            CompleteQuest(qInstance);
        }

        CommandReply(wizard.HasQuest(questName)
            ? $"Could not complete quest '{questName}'."
            : $"Completed quest '{questName}'.");
    }

    private void CommandRemoveQuest(Wizard wizard, string questName) {
        if (string.IsNullOrEmpty(questName)) {
            CommandReply("Give a quest name.");
            return;
        }

        var qInstance = wizard.QuestBehavior.CurrentQuestInstances
            .FirstOrDefault(q => q is not null && q.QuestName.Equals(questName, StringComparison.OrdinalIgnoreCase));
        var registryName = qInstance?.QuestName ?? questName;
        var wasActive = qInstance is not null;

        if (wasActive) {
            var questId = qInstance.ID;
            if (!wizard.RemoveQuest(qInstance.QuestName)) {
                CommandReply($"Could not remove quest '{questName}'.");
                return;
            }

            SendToSocket(new QUEST_MESSAGES_52_PROTOCOL.MSG_REMOVEQUEST { QuestID = questId });
        }

        var cleared = wizard.QuestBehavior.RemoveAllQuestRegistryEntries(registryName);
        if (cleared > 0) {
            WizardCollection.UpdateCharacterQuestBehavior(wizard);
        }

        if (!wasActive && cleared == 0) {
            CommandReply(QuestTemplateCollection.GetQuestByName(questName) is null
                ? $"Quest '{questName}' does not exist."
                : $"You have no record of quest '{questName}'.");
            return;
        }

        CommandReply($"Removed quest '{registryName}' ({(wasActive ? "was active" : "was not active")}, "
            + $"{cleared} registry entries cleared). It can be offered again.");
    }

    private void CommandGrantQuest(Wizard wizard, string questName, bool force) {
        if (string.IsNullOrEmpty(questName)) {
            CommandReply("Give a quest name.");
            return;
        }

        var template = QuestTemplateCollection.GetQuestByName(questName);
        if (template is null) {
            CommandReply($"Quest '{questName}' does not exist.");
            return;
        }
        if (wizard.HasQuest(template.m_questName)) {
            CommandReply($"You already have the quest '{template.m_questName}'.");
            return;
        }

        if (!force) {
            if (wizard.HasQuestRegistryValue(template.m_questName, QUEST_COMPLETED_ENTRY)) {
                CommandReply($"You already completed '{template.m_questName}'. Use '.quest remove' first, or '.quest forcegrant'.");
                return;
            }

            if (template.m_requirements is not null
                && !RequirementDispatcher.EvaluateRequirements(template.m_requirements,
                    new GenericRequirementContext(
                        requirements: template.m_requirements,
                        playerRef: SessionActor.ActorRef,
                        playerObj: GetActiveGameObject(),
                        wizard: wizard))) {
                CommandReply($"You do not meet the requirements for '{template.m_questName}'. Use '.quest forcegrant' to skip them.");
                return;
            }
        }

        StartQuest(template, wizard);
        CommandReply($"Granted quest '{template.m_questName}'{(force ? " (requirements skipped)" : "")}.");
    }

    private void StartQuest(QuestTemplate quest, Wizard wizard) {
        var questInstance = new QuestInstance(quest, wizard.CharId);
        wizard.AddQuest(questInstance);

        if (!_cachedQuestTemplates.Contains(quest)) {
            _cachedQuestTemplates.Add(quest);
        }

        SendQuestStartingMessage(quest, questInstance);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_COMBATWIN))]
    private void ReceiveCombatVictory(COMBAT_106_PROTOCOL.MSG_COMBATWIN message) {
        var wizard = GetActiveWizard();
        var foughtWithGroupMate = message.AllyCharIds?.Any(allyCharId => allyCharId != wizard.CharId
            && GroupRegistry.AreGrouped(wizard.CharId, allyCharId)) == true;

        foreach (var qInstance in wizard.QuestBehavior.CurrentQuestInstances.ToList()) {
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

                ProcessCombatGoal(wizard, qInstance, bountyGoal, message.MobAdjectives, foughtWithGroupMate);
            }
        }
    }

    private void SendQuestStartingMessage(QuestTemplate qTemplate, QuestInstance questInstance) {
        var qMadLibs = QuestMadlibs.GetMadLibForQuest(qTemplate);
        if (!_goalSerializer.Serialize(qMadLibs, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for quest '{0}'",
                Logger.Args(qTemplate.m_questName));

            return;
        }

        var rewards = GetQuestRewardsFromTemplate(qTemplate, null, SessionActor.ActorRef, GetActiveWizard());
        if (!_goalSerializer.Serialize(rewards, 1, out var serializedRewards)) {
            Logger.Error("Failed to serialize rewards for quest {0}.",
                Logger.Args(qTemplate.m_questName));

            return;
        }

        var associatedWorlds = GetAssociatedWorlds(qTemplate);
        if (!_goalSerializer.Serialize(associatedWorlds, 1, out var serializedWorlds)) {
            Logger.Error("Failed to serialize associated worlds for quest {0}.",
                Logger.Args(qTemplate.m_questName));

            return;
        }

        var qSendMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDQUEST {
            QuestID = questInstance.ID,
            QuestNameID = StringHash.Compute(qTemplate.m_questName),
            QuestType = 0, // ?
            QuestLevel = qTemplate.m_questLevel,
            QuestTitle = qTemplate.m_questTitle,
            QuestInfo = "", // ?
            New = 1,
            QuestMadlibs = madLibData,
            GoalData = "", // This field is empty per packet captures.
            Rewards = serializedRewards,
            ClientTags = "",
            AssociatedWorlds = serializedWorlds,
            NoQuestHelper = qTemplate.m_noQuestHelper ? (byte) 1 : (byte) 0,
            Mainline = qTemplate.m_mainline ? (byte) 1 : (byte) 0,
            ReadyToTurnIn = 0,
            SkipQHAutoSelect = 0,
            PetOnlyQuest = qTemplate.m_playAsYourPetNPC ? (byte) 1 : (byte) 0,
            ActivityType = 0, // ?
        };

        SendToSocket(qSendMsg);
        ShowQuestStartDialogue(qTemplate);

        // Send the quest starting goals now.
        foreach (var gTemplate in qTemplate.m_goals) {
            if (!qTemplate.m_startGoals.Contains(gTemplate.m_goalName)) {
                continue;
            }

            StartGoal(questInstance, gTemplate);
        }

        // Init quest start results.
        var startResults = qTemplate.m_startResults;
        ResultDispatcher.ExecuteResults(
            actorContext: Context,
            results: startResults,
            playerRef: SessionActor.ActorRef,
            playerObj: GetActiveGameObject(),
            zoneActor: SessionActor.GetZoneActor(),
            questName: questInstance.QuestName,
            skipWorldEffects: !ClaimInstanceStep(InstanceQuestClaimKind.QuestStart, questInstance.QuestName, "")
        );
    }

    private void SendQuestResumeMessage(QuestTemplate qTemplate, QuestInstance qInstance) {
        var qMadLibs = QuestMadlibs.GetMadLibForQuest(qTemplate);
        if (!_goalSerializer.Serialize(qMadLibs, 1, out var madLibData)) {
            Logger.Error("Failed to serialize madlib data for quest '{0}'",
                Logger.Args(qTemplate.m_questName));

            return;
        }

        var rewards = GetQuestRewardsFromTemplate(qTemplate, null, SessionActor.ActorRef, GetActiveWizard());
        if (!_goalSerializer.Serialize(rewards, 1, out var serializedRewards)) {
            Logger.Error("Failed to serialize rewards for quest {0}.",
                Logger.Args(qTemplate.m_questName));

            return;
        }

        var associatedWorlds = GetAssociatedWorlds(qTemplate);
        if (!_goalSerializer.Serialize(associatedWorlds, 1, out var serializedWorlds)) {
            Logger.Error("Failed to serialize associated worlds for quest {0}.",
                Logger.Args(qTemplate.m_questName));

            return;
        }

        var qSendMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDQUEST {
            QuestID = qInstance.ID,
            QuestNameID = StringHash.Compute(qTemplate.m_questName),
            QuestType = 0, // ?
            QuestLevel = qTemplate.m_questLevel,
            QuestTitle = qTemplate.m_questTitle,
            QuestInfo = "", // ?
            New = 0,
            QuestMadlibs = madLibData,
            GoalData = "",
            Rewards = serializedRewards,
            ClientTags = "",
            AssociatedWorlds = serializedWorlds,
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

            SendGoalMessage(gTemplate, qInstance, forceSendDestZone: true);
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
                if (waypointGoal.m_zoneTag != currentZone) {
                    // Player is not in the correct zone for this goal.
                    continue;
                }

                TryCompleteZoneEntryGoal(qInstance, gTemplate, wizard);
            }
        }
    }

    private void CheckForWaypointGoalZoneExit(Wizard wizard) {
        var previousZone = wizard.PreviousZone;

        foreach (var quest in _cachedQuestTemplates) {
            // Check to see if the quest has any waypoint goals that trigger on zone exit.
            var waypointGoals = quest.m_goals
                .Where(gTemplate => gTemplate.m_goalType == GOAL_TYPE.GOAL_TYPE_WAYPOINT)
                .Where(wTemplate => wTemplate is WaypointGoalTemplate wGoalTemplate
                    && wGoalTemplate.m_zoneExit);

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

            // Mark each of the goals complete if the player has exited the zone.
            foreach (var gTemplate in activeWaypointGoals) {
                if (gTemplate is not WaypointGoalTemplate waypointGoal) {
                    continue;
                }
                if (waypointGoal.m_zoneTag != previousZone) {
                    // Player did not exit the correct zone for this goal.
                    continue;
                }

                CompleteGoal(qInstance, gTemplate);
            }
        }
    }

    private void StartGoal(QuestInstance questInstance, GoalTemplate goalTemplate) {
        var wizard = GetActiveWizard();

        // Goals with requirements only activate for wizards that meet them (e.g. per-school goals).
        if (goalTemplate.m_goalRequirements is not null
            && goalTemplate.m_goalRequirements.m_requirements is not null
            && goalTemplate.m_goalRequirements.m_requirements.Count > 0) {
            var requirementsMet = RequirementDispatcher.EvaluateRequirements(
                requirements: goalTemplate.m_goalRequirements,
                context: new QuestRequirementContext(
                    goalTemplate.m_goalRequirements,
                    SessionActor.ActorRef,
                    GetActiveGameObject(),
                    wizard,
                    questInstance.QuestName,
                    goalTemplate.m_goalName
                )
            );
            if (!requirementsMet) {
                return;
            }
        }

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
            zoneActor: SessionActor.GetZoneActor(),
            questName: questInstance.QuestName,
            goalName: goalTemplate.m_goalName,
            skipWorldEffects: !ClaimInstanceStep(InstanceQuestClaimKind.GoalStart, questInstance.QuestName, goalTemplate.m_goalName)
        );

        // Play goal start dialogue if it exists.
        var goalId = questInstance.GoalProgress.First(g => g.GoalName == goalTemplate.m_goalName).ID;
        ShowGoalStartDialogue(goalTemplate, questInstance.ID, goalId);

        // A zone-entry goal that starts while the player is already in its zone would otherwise wait for
        // the next zone attach. Complete it now, after the start message, as the attach path does.
        TryCompleteZoneEntryGoal(questInstance, goalTemplate, wizard);
    }

    private void TryCompleteZoneEntryGoal(QuestInstance questInstance, GoalTemplate goalTemplate, Wizard wizard) {
        if (goalTemplate is not WaypointGoalTemplate waypointGoal || !waypointGoal.m_zoneEntry) {
            return;
        }
        if (waypointGoal.m_zoneTag != wizard.Zone) {
            return;
        }
        // Guards against double completion (already completed or never started).
        if (!questInstance.IsGoalActive(goalTemplate.m_goalName)) {
            return;
        }

        CompleteGoal(questInstance, goalTemplate);
    }

    private void CompleteGoal(QuestInstance questInstance, GoalTemplate goalTemplate, bool showCompletionDialogue = true,
        GoalCompletion completion = GoalCompletion.Own) {
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
        if (completion != GoalCompletion.CatchUp) {
            if (showCompletionDialogue) {
                ShowGoalCompletionDialogue(goalTemplate, questInstance.ID, gInstance.ID);
            }

            // The player who completes it first tells the instance; the others run only their personal results.
            var first = completion == GoalCompletion.Own
                && ClaimInstanceStep(InstanceQuestClaimKind.GoalComplete, questInstance.QuestName, goalTemplate.m_goalName);
            ResultDispatcher.ExecuteResults(
                actorContext: Context,
                results: goalTemplate.m_completeResults,
                playerRef: SessionActor.ActorRef,
                playerObj: GetActiveGameObject(),
                zoneActor: SessionActor.GetZoneActor(),
                questName: questInstance.QuestName,
                goalName: goalTemplate.m_goalName,
                skipWorldEffects: !first,
                xpScale: GetRunXpScale(wizard, questInstance.QuestName)
            );

            if (first) {
                PostGoalCompleteEvent(questInstance.QuestName, goalTemplate.m_goalName);
            }
        }

        var qTemplate = _cachedQuestTemplates.FirstOrDefault(q => q.m_questName == questInstance.QuestName);
        if (qTemplate == null) {
            Logger.Error("Failed to find quest template for quest '{0}' when completing goal '{1}'",
                Logger.Args(questInstance.QuestName, goalTemplate.m_goalName));
            return;
        }

        if (!DetermineNextGoals(qTemplate, questInstance, out var gTemplates)) {
            if (completion != GoalCompletion.CatchUp) {
                CompleteQuest(questInstance);
            }

            return;
        }

        foreach (var g in gTemplates) {
            StartGoal(questInstance, g);
        }
    }

    // Zone triggers listen for this event to react to a goal (an NPC walks to a new spot, a barricade falls).
    private void PostGoalCompleteEvent(string questName, string goalName) {
        var zoneActor = SessionActor.GetZoneActor();
        if (zoneActor is null) {
            return;
        }

        zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
            EventName = $"{GOAL_COMPLETE_EVENT_PREFIX}{questName}_{goalName}",
            PlayerActor = SessionActor.ActorRef,
            PlayerGameObject = GetActiveGameObject(),
        });
    }

    private void CompleteQuest(QuestInstance questInstance) {
        var wizard = GetActiveWizard();

        var xpScale = GetRunXpScale(wizard, questInstance.QuestName);
        var runCount = IsZoneDungeonQuest(wizard, questInstance.QuestName)
            ? wizard.GetQuestRegistryValue(questInstance.QuestName, QUEST_COMPLETED_ENTRY)
            : 0UL;

        if (!wizard.CompleteQuest(questInstance.QuestName)) {
            Logger.Error("Failed to complete quest '{0}' for player '{1}'",
                Logger.Args(questInstance.QuestName, wizard.CharId));
            return;
        }

        // The "Complete" entry counts this player's completions of a dungeon quest.
        if (IsZoneDungeonQuest(wizard, questInstance.QuestName)) {
            wizard.SetQuestRegistryValue(questInstance.QuestName, QUEST_COMPLETED_ENTRY, runCount + 1);
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
            zoneActor: SessionActor.GetZoneActor(),
            questName: questInstance.QuestName,
            skipWorldEffects: !ClaimInstanceStep(InstanceQuestClaimKind.QuestComplete, questInstance.QuestName, ""),
            xpScale: xpScale
        );

        // Fire the "you have learned a new spell!" cinematic for the spell rewards. Only the spells
        // go here; the gold/XP/item popup is already sent by the drop table handlers (MSG_LOOT).
        var spellRewards = new LootInfoList {
            m_loot = []
        };
        AppendSpellRewards(spellRewards.m_loot, qTemplate, wizard);
        if (spellRewards.m_loot.Count > 0
            && _goalSerializer.Serialize(spellRewards, 1, out var spellRewardData)) {
            SendToSocket(new WIZARD_12_PROTOCOL.MSG_QUESTREWARDS {
                QuestID = questInstance.ID,
                LootList = spellRewardData
            });
        }

        // Chain advance: completion stamps the "Complete" registry entry, which is the ReqHasEntry
        // prerequisite of the next quest in the dungeon's chain.
        TryGrantDungeonQuests(wizard);
    }

    private void SendGoalMessage(GoalTemplate gTemplate, QuestInstance qInstance, byte sendType = 0, bool forceSendDestZone = true) {
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
        var tagList = GetClientTagList(gTemplate.m_clientTags?.Select(t => (string)t).ToArray() ?? []);
        var newSerializer = new ObjectSerializer(false);
        if (!newSerializer.Serialize(tagList, 1, out var clientTagData)) {
            Logger.Error("Failed to serialize client tag data for goal '{0}' in quest '{1}'",
                Logger.Args(gTemplate.m_goalName, qInstance.QuestName));

            clientTagData = string.Empty;
        }
        // Or, send no data if the client tag list has no entries.
        if (tagList.m_clientTags.Count <= 0) {
            clientTagData = string.Empty;
        }

        var patronIcon = GetPatronIconFromGoal(gTemplate);

        // Determine the destination zone. Do not send it if the player is currently in that zone.
        var wizard = GetActiveWizard();
        var currentZone = wizard.Zone ?? "";
        string destZone = gTemplate.m_destinationZone;
        if (!forceSendDestZone
            && !string.IsNullOrEmpty(destZone)
            && destZone.Equals(currentZone, StringComparison.OrdinalIgnoreCase)) {
            destZone = "";
        }

        var packet = new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDGOAL {
            QuestID = qInstance.ID,
            GoalID = gInstance.ID,
            GoalNameID = gTemplate.m_goalNameID,
            GoalTitle = gTemplate.m_goalTitle,
            GoalLocation = gTemplate.m_locationName,
            GoalDestinationZone = (sendType == 2) ? "" : destZone,
            GoalImage1 = gTemplate.m_displayImage1,
            GoalImage2 = gTemplate.m_displayImage2,
            PersonaName = "",                    // match capture
            PatronIcon = patronIcon,
            GoalType = (byte) gTemplate.m_goalType,
            GoalStatus = 0,                      // *** ACTIVE on retail captures ***
            GoalCount = gInstance.CurrentProgress,
            UseTally = (byte) (gTemplate.m_tallyCounter is not null ? 1 : 0),
            GoalTotal = gTemplate.m_tallyCounter?.m_count ?? 0,
            TallyText = gTemplate.m_tallyCounter?.m_descriptor ?? "",
            SubscriberGoalTotal = gTemplate.m_tallyCounter?.m_count ?? 0,
            SendType = sendType,                 // 0=resume/start-of-zone, 1=start, 2=progress
            GoalMadlibs = madLibData,
            ClientTags = clientTagData,
            NoQuestHelper = gTemplate.m_noQuestHelper ? (byte) 1 : (byte) 0,
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
                                   string[] defeatedMobAdjectives,
                                   bool foughtWithGroupMate) {
        var shouldIncrement = goalTemplate.m_goalType == GOAL_TYPE.GOAL_TYPE_BOUNTY;
        var goalMobAdjectives = goalTemplate.m_npcAdjectives ?? [];

        // Check to see if the defeated mob matches the goal's NPC adjectives.
        if (!defeatedMobAdjectives.Any(adjective => goalMobAdjectives.Contains(adjective))) {
            return;
        }

        // If there were, determine how many of the adjectives matched.
        // We'll increment by that amount.
        var matchCount = defeatedMobAdjectives.Count(adjective => goalMobAdjectives.Contains(adjective));

        if (goalTemplate.m_goalType == GOAL_TYPE.GOAL_TYPE_BOUNTYCOLLECT) {
            var chance = goalTemplate.m_tallyCounter?.m_percentChance ?? DEFAULT_KILL_COLLECT_CHANCE;
            shouldIncrement = new Random().NextDouble() <= chance;

            // todo: live's bonus size is unknown; a group win gets one more roll at the same chance.
            if (!shouldIncrement && foughtWithGroupMate && new Random().NextDouble() <= chance) {
                shouldIncrement = true;
                SendToSocket(new WIZARD_12_PROTOCOL.MSG_GROUPQUESTCREDIT());
            }
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

        // Spell rewards live in the results, not the drop tables; surface the wizard's own school spell.
        convertedResults.m_loot ??= [];
        AppendSpellRewards(convertedResults.m_loot, qTemplate, playerWizard);

        return convertedResults;
    }

    /// <summary>
    /// Adds the quest's spell rewards to a loot list for the reward previews. Gated results
    /// are evaluated through the single requirement engine, so a wizard only sees their
    /// own school's spell; without a wizard, gated results are skipped.
    /// </summary>
    internal static void AppendSpellRewards(List<LootInfo> loot, QuestTemplate qTemplate, Wizard playerWizard) {
        foreach (var learnSpell in qTemplate.m_endResults.m_results.OfType<ResLearnSpell>()) {
            if (learnSpell.m_templateID == 0) {
                continue;
            }

            if (learnSpell.m_requirements is not null) {
                if (playerWizard is null) {
                    continue;
                }

                var requirementContext = new GenericRequirementContext(
                    requirements: learnSpell.m_requirements,
                    playerRef: null,
                    playerObj: null,
                    wizard: playerWizard
                );
                if (!RequirementDispatcher.EvaluateRequirements(learnSpell.m_requirements, requirementContext)) {
                    continue;
                }
            }

            loot.Add(new AddSpellLootInfo {
                m_lootType = LOOT_TYPE.LOOT_TYPE_ADD_SPELL,
                m_spellID = learnSpell.m_templateID,
            });
        }
    }

    private static AssociatedWorldsList GetAssociatedWorlds(QuestTemplate qTemplate) {
        var worlds = qTemplate.m_goals
            .Select(g => (string)g.m_destinationZone)
            .Where(z => !string.IsNullOrWhiteSpace(z))
            .Select(z => z.Split('/')[0])                 // "MyWorld/HubZone..." -> "MyWorld"
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Fallback: if the quest has no explicit destination zones but clearly belongs
        // to a world (e.g., via zone-entry waypoint), you can infer from m_zoneTag roots.
        if (worlds.Length == 0) {
            worlds = [.. qTemplate.m_goals
                .OfType<WaypointGoalTemplate>()
                .Select(w => (string)w.m_zoneTag)
                .Where(z => !string.IsNullOrWhiteSpace(z))
                .Select(z => z.Split('/')[0])
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        var associatedWorlds = new AssociatedWorldsList {
            m_associatedWorlds = [.. worlds]
        };

        return associatedWorlds;
    }

    private void ShowQuestStartDialogue(QuestTemplate questTemplate)
         => ShowDialogue(questTemplate.m_dialogList, "Start", "QuestStart");

    private void ShowGoalStartDialogue(GoalTemplate goalTemplate, ulong questId, ulong goalId)
        => ShowDialogue(goalTemplate.m_dialogList, "Prep", "QuestStart", questId, goalId);

    private void ShowGoalCompletionDialogue(GoalTemplate goalTemplate, ulong questId, ulong goalId)
        => ShowDialogue(goalTemplate.m_dialogList, GOAL_COMPLETION_DIALOG_TAG, "Completion", questId, goalId);

    private void ShowQuestCompletionDialogue(QuestTemplate questTemplate)
        => ShowDialogue(questTemplate.m_dialogList, "Complete", "QuestComplete");

    private void ShowDialogue(ActorDialogListBase dialogListBase, string dialogTag, string completionType, ulong questId = 0, ulong goalId = 0) {
        var dialog = FindDialogue(dialogListBase, dialogTag);
        if (dialog != null) {
            SendActorDialog(dialog, completionType, questId, goalId);
        }
    }

    private static ActorDialog FindDialogue(ActorDialogListBase dialogListBase, string dialogTag)
        => dialogListBase is ActorDialogList dialogList
            ? dialogList.m_dialogs?.FirstOrDefault(de => de.m_dialogTag == dialogTag)
            : null;

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

    private void RemoveDungeonQuestsOutsideZone(Wizard wizard) {
        // A relog into the same zone never arrives with a different zone, so its dungeon quests stay.
        if (wizard?.QuestBehavior is null) {
            return;
        }

        // Inside an instance the dungeon's quests belong to every zone of its container; without an answer
        // from the zone nothing is removed.
        var progress = QueryInstanceProgress();
        if (progress is null) {
            return;
        }

        var keep = DungeonQuestIndex.GetQuestsForZones(progress.IsInstance ? progress.Zones : [wizard.Zone])
            .Select(t => (string)t.m_questName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = wizard.QuestBehavior.CurrentQuestInstances
            .Where(q => DungeonQuestIndex.IsDungeonQuest(q.QuestName) && !keep.Contains(q.QuestName))
            .ToList();

        foreach (var qInstance in stale) {
            // Progress kept in the registry goes too; the next grant starts fresh.
            wizard.QuestBehavior.RemoveAllQuestRegistryEntries(qInstance.QuestName, keepComplete: true);
            if (!RemoveQuestEverywhere(wizard, qInstance.QuestName)) {
                continue;
            }

            Logger.Information("Removed dungeon quest '{0}' from {1}: left its zone (now in '{2}').",
                Logger.Args(qInstance.QuestName, wizard.CharId, wizard.Zone));
        }
    }

    private void TryGrantDungeonQuests(Wizard wizard, bool reconcileWithInstance = false) {
        if (wizard is null) {
            return;
        }

        var progress = QueryInstanceProgress();
        var inInstance = progress?.IsInstance == true;
        var instanceZones = inInstance ? progress.Zones : null;
        var templates = DungeonQuestIndex.GetQuestsForZones(instanceZones ?? [wizard.Zone]);
        if (templates.Count == 0) {
            return;
        }

        var completedQuests = (progress?.CompletedQuests ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var zoneQuestNames = templates.Select(t => (string)t.m_questName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool Eligible(QuestTemplate template) {
            // An instance runs each quest once, whatever the player completed in earlier runs.
            var done = inInstance
                ? completedQuests.Contains(template.m_questName)
                : wizard.HasQuestRegistryValue(template.m_questName, QUEST_COMPLETED_ENTRY);
            if (done) {
                return false;
            }

            if (template.m_requirements is null) {
                return true;
            }

            var context = new GenericRequirementContext(
                requirements: template.m_requirements,
                playerRef: null,
                playerObj: null,
                wizard: wizard) {
                InstanceZones = instanceZones,
                InstanceQuestCompleted = inInstance
                    ? name => zoneQuestNames.Contains(name) ? completedQuests.Contains(name) : null
                    : null,
            };

            return RequirementDispatcher.EvaluateRequirements(template.m_requirements, context);
        }

        foreach (var template in templates) {
            var saved = wizard.QuestBehavior.CurrentQuestInstances.FirstOrDefault(q => q.QuestName == template.m_questName);
            if (saved is not null) {
                // The instance's progress wins over what the player saved for the quest.
                if (!(reconcileWithInstance && inInstance)) {
                    continue;
                }

                var savedGoals = saved.GoalProgress.Where(g => g.IsGoalCompleted()).Select(g => g.GoalName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var instanceGoals = progress.CompletedGoals.TryGetValue(template.m_questName, out var goals)
                    ? goals : [];
                if (Eligible(template) && savedGoals.SetEquals(instanceGoals)) {
                    continue;
                }

                DropSavedDungeonQuest(wizard, saved);
            }

            if (!Eligible(template)) {
                continue;
            }

            var questInstance = new QuestInstance(template, wizard.CharId);
            if (!wizard.AddQuest(questInstance)) {
                continue;
            }

            if (!_cachedQuestTemplates.Contains(template)) {
                _cachedQuestTemplates.Add(template);
            }

            SendQuestStartingMessage(template, questInstance);

            Logger.Information("Granted dungeon quest '{0}' to {1} in '{2}'.",
                Logger.Args(template.m_questName, wizard.CharId, wizard.Zone));

            // A player who joins an instance midway continues where it is.
            if (inInstance && progress.CompletedGoals.TryGetValue(template.m_questName, out var completedGoals)) {
                foreach (var goalName in completedGoals) {
                    var goalTemplate = template.m_goals.FirstOrDefault(g => g.m_goalName == goalName);
                    if (goalTemplate is not null && questInstance.IsGoalActive(goalName)) {
                        CompleteGoal(questInstance, goalTemplate, showCompletionDialogue: false,
                            completion: GoalCompletion.CatchUp);
                    }
                }
            }
        }
    }

    private void DropSavedDungeonQuest(Wizard wizard, QuestInstance saved) {
        wizard.QuestBehavior.RemoveAllQuestRegistryEntries(saved.QuestName, keepComplete: true);
        if (!RemoveQuestEverywhere(wizard, saved.QuestName)) {
            return;
        }

        Logger.Information("Replaced saved dungeon quest '{0}' of {1} with the instance's progress.",
            Logger.Args(saved.QuestName, wizard.CharId));
    }

    // Removes the quest and tells the client about every copy of it the player holds.
    private bool RemoveQuestEverywhere(Wizard wizard, string questName) {
        var questIds = wizard.QuestBehavior.CurrentQuestInstances
            .Where(q => q is not null && q.QuestName == questName)
            .Select(q => q.ID)
            .ToList();
        if (!wizard.RemoveQuest(questName)) {
            return false;
        }

        foreach (var questId in questIds) {
            SendToSocket(new QUEST_MESSAGES_52_PROTOCOL.MSG_REMOVEQUEST {
                QuestID = questId,
            });
        }

        return true;
    }

    private bool IsZoneDungeonQuest(Wizard wizard, string questName)
        => DungeonQuestIndex.GetQuestsForZones(GetInstanceZones(wizard))
            .Any(t => string.Equals(t.m_questName, questName, StringComparison.OrdinalIgnoreCase));

    // Quest XP shrinks with the player's completions of a dungeon quest: full, half, then none.
    private float GetRunXpScale(Wizard wizard, string questName) {
        if (!IsZoneDungeonQuest(wizard, questName)) {
            return 1f;
        }

        return wizard.GetQuestRegistryValue(questName, QUEST_COMPLETED_ENTRY) switch {
            0 => FIRST_RUN_XP_SCALE,
            1 => SECOND_RUN_XP_SCALE,
            _ => LATER_RUN_XP_SCALE,
        };
    }

    // True when the step is the instance's first or the quest is not shared; the world effects of a step run once.
    private bool ClaimInstanceStep(InstanceQuestClaimKind kind, string questName, string goalName) {
        var zoneActor = SessionActor.GetZoneActor();
        if (zoneActor is null || !IsZoneDungeonQuest(GetActiveWizard(), questName)) {
            return true;
        }

        try {
            return zoneActor.Ask<ZONE_102_PROTOCOL.MSG_CLAIMINSTANCEQUESTRSP>(
                new ZONE_102_PROTOCOL.MSG_CLAIMINSTANCEQUEST {
                    Kind = kind,
                    QuestName = questName,
                    GoalName = goalName,
                    Origin = SessionActor.ActorRef,
                }, INSTANCE_QUERY_TIMEOUT).Result.First;
        }
        catch (Exception ex) {
            Logger.Error("Instance quest claim for '{0}' failed: {1}", Logger.Args(questName, ex.Message));

            return true;
        }
    }

    // The zones whose dungeon quests apply to the player: the container's zones in an instance, else the zone itself.
    private IReadOnlyCollection<string> GetInstanceZones(Wizard wizard) {
        var progress = QueryInstanceProgress();

        return progress?.IsInstance == true ? progress.Zones : [wizard.Zone];
    }

    private ZONE_102_PROTOCOL.MSG_QUERYINSTANCEQUESTSRSP QueryInstanceProgress() {
        var zoneActor = SessionActor.GetZoneActor();
        if (zoneActor is null) {
            return null;
        }

        try {
            return zoneActor.Ask<ZONE_102_PROTOCOL.MSG_QUERYINSTANCEQUESTSRSP>(
                new ZONE_102_PROTOCOL.MSG_QUERYINSTANCEQUESTS(), INSTANCE_QUERY_TIMEOUT).Result;
        }
        catch (Exception ex) {
            Logger.Error("Instance quest query failed: {0}", Logger.Args(ex.Message));

            return null;
        }
    }

}
