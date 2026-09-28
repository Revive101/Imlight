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
 * CINEMATIC SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Plays cinematic actor scenes (ResCinematicActor) for this player: spawns the actor, walks it through
 * the states of its state set and plays the dialog the cinematic template pairs with each state.
 *
 * USAGE EXAMPLE:
 * The ResCinematicActor handler sends MSG_PLAYCINEMATICACTOR to the player's session.
 *
 * NOTE:
 * The template (CinematicActorFactory, by m_cinematicName) names the actor's state set, the player's game state
 * during the scene, a dialog per state (tagged with the state name) whose m_dialogEvents fire when the
 * client closes it, and the state changes those events cause. States move on by those events or by
 * the state set's auto transitions. One scene plays at a time; a zone change ends it.
 *
 * Live shows the actor and the state changes (the actor's and the triggering player's) to everyone in the
 * zone, and the dialogs only to the triggering player.
 * 
 * TODO:
 * - m_router is not read; every scene is shown zone-wide (all 64 client triggers use ROUTING_ZONE).
 * - Player emotes of m_stateInteractions (m_playerEmote) are not played.
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/27/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.Cryptography;
using Imcodec.Math;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Cinematics;
using Imlight.CoreLib.Game.States;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;

namespace Imlight.CoreLib.Game.Services;

internal class CinematicService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const string DIALOG_COMPLETION_TYPE = "None";
    private const string DIALOG_ENTRY_EVENT_COMPLETION = "ENTRY";
    private const string PLAYER_RELEASE_STATE = "Idle";
    private const int OPENING_DIALOG_SENDS = 2;

    private static readonly TimeSpan DIALOG_TIMEOUT = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ACTOR_REMOVE_DELAY = TimeSpan.FromSeconds(1);
    private static readonly PropertyFlags OBJECT_PROPERTY_FLAGS = PropertyFlags.Prop_Public
                                                                | PropertyFlags.Prop_Transmit
                                                                | PropertyFlags.Prop_AuthorityTransmit;

    private readonly CoreObjectSerializer _objectSerializer = new(
        versionable: false,
        behaviors: SerializerFlags.None
    );
    private readonly ObjectSerializer _dialogSerializer = new(Versionable: false);

    private ActiveCinematic _active;
    private int _nextRunId;

    private sealed class ActiveCinematic {

        public int RunId { get; init; }
        public WizCinematicActorTemplate Template { get; init; }
        public Dictionary<string, ObjState> States { get; init; }
        public GID ActorId { get; init; }
        public GID PlayerId { get; init; }
        public IActorRef ZoneActor { get; init; }
        public string StateName { get; set; }
        public List<string> PendingDialogEvents { get; set; } = [];
        public bool PlayerReleased { get; set; }

    }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new CinematicService(parentActor));

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_PLAYCINEMATICACTOR))]
    private void ReceivePlayCinematicActor(CHARACTER_103_PROTOCOL.MSG_PLAYCINEMATICACTOR message) {
        var cinematic = message.Cinematic;
        string cinematicName = cinematic?.m_cinematicName;
        if (string.IsNullOrEmpty(cinematicName)) {
            return;
        }

        if (_active is not null) {
            Logger.Debug("Cinematic {Name} not played: {Current} is still playing.",
                Logger.Args(cinematicName, _active.Template.m_cinematicName));

            return;
        }

        var template = CinematicActorFactory.GetTemplate(cinematicName);
        if (template is null) {
            Logger.Warning("Cinematic template {Name} could not be loaded.", Logger.Args(cinematicName));

            return;
        }

        var category = StateFactory.GetStateSet(template.m_actorStateSet ?? "")?.m_categories?
            .FirstOrDefault(c => c?.m_states is { Count: > 0 });
        if (category is null || string.IsNullOrEmpty(category.m_startState)) {
            Logger.Warning("Cinematic {Name} has no usable state set {Set}.",
                Logger.Args(cinematicName, template.m_actorStateSet));

            return;
        }

        var player = GetActiveGameObject();
        var playerLocation = player.m_location;
        var start = cinematic.m_startAtActor ? playerLocation : cinematic.m_startLoc;
        var target = cinematic.m_endAtActor ? playerLocation : cinematic.m_endLoc;

        var actor = BuildActor((uint) cinematic.m_objectTemplateID, template, start, target);
        if (actor is null) {
            return;
        }

        if (!_objectSerializer.Serialize(actor, OBJECT_PROPERTY_FLAGS, out var actorData)) {
            Logger.Error("Failed to serialize the actor of cinematic {Name}.", Logger.Args(cinematicName));

            return;
        }

        _active = new ActiveCinematic {
            RunId = ++_nextRunId,
            Template = template,
            States = category.m_states
                .Where(s => s is not null && !string.IsNullOrEmpty(s.m_stateName))
                .GroupBy(s => s.m_stateName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal),
            ActorId = actor.m_globalID,
            PlayerId = player.m_globalID,
            ZoneActor = message.ZoneActor,
        };

        SendToZone(new GAME_5_PROTOCOL.MSG_NEWOBJECT {
            Data = actorData
        });

        if (!string.IsNullOrEmpty(template.m_targetGameState)) {
            SendEnterState(_active.PlayerId, template.m_targetGameState);
        }

        // The actor is created in its start state; only later states are announced. Live sends the
        // opening dialog twice, 1 ms apart.
        EnterActorState(category.m_startState, announce: false, dialogSends: OPENING_DIALOG_SENDS);
    }

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_COMPLETEDIALOG))]
    private void ReceiveCompleteDialog(WIZARD_12_PROTOCOL.MSG_COMPLETEDIALOG message) {
        // CompletionType "ENTRY" reports an entry's dialog event mid-dialog; an empty one closes the dialog.
        string completionType = message.CompletionType;
        if (string.Equals(completionType, DIALOG_ENTRY_EVENT_COMPLETION, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        if (_active is null || _active.PendingDialogEvents.Count == 0) {
            return;
        }

        FireDialogEvents();
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_CINEMATICAUTOSTATE))]
    private void ReceiveAutoState(CHARACTER_103_PROTOCOL.MSG_CINEMATICAUTOSTATE message) {
        if (_active is null || _active.RunId != message.RunId || _active.StateName != message.StateName) {
            return;
        }

        if (_active.States.TryGetValue(message.StateName, out var state) && !string.IsNullOrEmpty(state.m_autoState)) {
            EnterActorState(state.m_autoState, announce: true);
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_CINEMATICDIALOGTIMEOUT))]
    private void ReceiveDialogTimeout(CHARACTER_103_PROTOCOL.MSG_CINEMATICDIALOGTIMEOUT message) {
        if (_active is null || _active.RunId != message.RunId || _active.StateName != message.StateName
            || _active.PendingDialogEvents.Count == 0) {
            return;
        }

        Logger.Warning("No dialog close reported for state {State} of cinematic {Name} after {Timeout}; continuing.",
            Logger.Args(message.StateName, _active.Template.m_cinematicName, DIALOG_TIMEOUT));

        FireDialogEvents();
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_CINEMATICREMOVEACTOR))]
    private void ReceiveRemoveActor(CHARACTER_103_PROTOCOL.MSG_CINEMATICREMOVEACTOR message) {
        if (_active is null || _active.RunId != message.RunId) {
            return;
        }

        SendToZone(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = _active.ActorId
        });
        _active = null;
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PRELOGIN))]
    private void ReceivePreLogin(ZONE_102_PROTOCOL.MSG_PRELOGIN message) {
        // This client drops the actor with the old zone; the players still there are told to drop it too.
        if (_active is not null) {
            Logger.Debug("Cinematic {Name} ended by a zone change.", Logger.Args(_active.Template.m_cinematicName));
            _active.ZoneActor?.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
                Message = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = _active.ActorId },
                Selfless = true,
                Sender = SessionActor.ActorRef,
                Targets = ZoneBroadcastTarget.Players,
            });
        }

        Timers.CancelAll();
        _active = null;
    }

    private void EnterActorState(string stateName, bool announce, int dialogSends = 1) {
        var run = _active;
        run.StateName = stateName;
        run.PendingDialogEvents = [];

        if (announce) {
            SendEnterState(run.ActorId, stateName);
        }

        foreach (var interaction in run.Template.m_stateInteractions ?? []) {
            if (interaction?.m_actorState != stateName || string.IsNullOrEmpty(interaction.m_playerCinematicState)) {
                continue;
            }

            SendEnterState(run.PlayerId, interaction.m_playerCinematicState);
            run.PlayerReleased = true;
        }

        var dialog = (run.Template.m_dialogList as ActorDialogList)?.m_dialogs?
            .FirstOrDefault(d => d?.m_dialogTag == stateName);
        if (dialog is not null) {
            for (var i = 0; i < dialogSends; i++) {
                SendDialog(dialog);
            }

            run.PendingDialogEvents = dialog.m_dialogEvents?.Where(e => !string.IsNullOrEmpty(e)).ToList() ?? [];
            if (run.PendingDialogEvents.Count > 0) {
                Timers.StartSingleTimer(
                    $"cinematic-dialog-{run.RunId}",
                    new CHARACTER_103_PROTOCOL.MSG_CINEMATICDIALOGTIMEOUT { RunId = run.RunId, StateName = stateName },
                    DIALOG_TIMEOUT);
            }
        }

        run.States.TryGetValue(stateName, out var state);
        if (state is { m_autoTransition: true } && !string.IsNullOrEmpty(state.m_autoState)) {
            Timers.StartSingleTimer(
                $"cinematic-auto-{run.RunId}",
                new CHARACTER_103_PROTOCOL.MSG_CINEMATICAUTOSTATE { RunId = run.RunId, StateName = stateName },
                TimeSpan.FromSeconds(Math.Max(0f, state.m_transitionTime)));

            return;
        }

        // Only a dialog event or an auto transition moves the actor on; a state with neither ends the scene.
        if (run.PendingDialogEvents.Count == 0) {
            FinishCinematic(run);
        }
    }

    private void FireDialogEvents() {
        var run = _active;
        var events = run.PendingDialogEvents;
        run.PendingDialogEvents = [];
        Timers.Cancel($"cinematic-dialog-{run.RunId}");

        foreach (var dialogEvent in events) {
            var change = run.Template.m_stateChangeEvents?.FirstOrDefault(c =>
                c is not null
                && c.m_event == dialogEvent
                && (string.IsNullOrEmpty(c.m_requiredState) || c.m_requiredState == run.StateName));
            if (change is null || string.IsNullOrEmpty(change.m_newState)) {
                continue;
            }

            EnterActorState(change.m_newState, announce: true);

            return;
        }

        Logger.Debug("Cinematic {Name}: no state change for events [{Events}] in state {State}; ending it.",
            Logger.Args(run.Template.m_cinematicName, string.Join(",", events), run.StateName));
        FinishCinematic(run);
    }

    private void FinishCinematic(ActiveCinematic run) {
        if (!run.PlayerReleased) {
            SendEnterState(run.PlayerId, PLAYER_RELEASE_STATE);
            run.PlayerReleased = true;
        }

        Timers.StartSingleTimer(
            $"cinematic-remove-{run.RunId}",
            new CHARACTER_103_PROTOCOL.MSG_CINEMATICREMOVEACTOR { RunId = run.RunId },
            ACTOR_REMOVE_DELAY);
    }

    private void SendEnterState(GID objectId, string stateName)
        => SendToZone(new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            GameObjectID = objectId,
            State = StringHash.Compute(stateName),
        });

    private void SendToZone(IMessage message) {
        // Straight to this client first, so its messages keep their order with the dialogs.
        SendToSocket(message);

        _active?.ZoneActor?.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = message,
            Selfless = true,
            Sender = SessionActor.ActorRef,
            Targets = ZoneBroadcastTarget.Players,
        });
    }

    private void SendDialog(ActorDialog dialog) {
        if (!_dialogSerializer.Serialize(dialog, 16, out var dialogData)) {
            Logger.Error("Failed to serialize cinematic dialog {Tag}.", Logger.Args(dialog.m_dialogTag));

            return;
        }

        SendToSocket(new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = 0,
            QuestID = 0,
            GoalID = 0,
            CompletionType = DIALOG_COMPLETION_TYPE,
            ActorDialog = dialogData,
            Persona = "",
            PersonaName = "",
            PersonaIcon = "",
        });
    }

    private static WizClientObject BuildActor(uint templateId, WizCinematicActorTemplate cinematic,
                                              Vector3 start, Vector3 target) {
        if (CoreObjectFactory.GetCoreTemplate(templateId) is not GameObjectTemplate actorTemplate) {
            Logger.Warning("Cinematic actor template {Id} could not be loaded.", Logger.Args(templateId));

            return null;
        }

        var actor = CoreObjectFactory.InitializeCoreObjectBehaviors(new WizClientObject {
            m_globalID = RandomGen.GenerateGUID(),
            m_templateID = templateId,
            m_location = start,
            m_fScale = 1,
            m_debugName = actorTemplate.m_objectName ?? "",
        }, actorTemplate);

        // The actor plays the cinematic's state set, not the template's own (NPCMobileStates).
        if (CoreObjectFactory.FindBehaviorInstance<ObjectStateBehavior>(actor, out var stateBehavior)) {
            stateBehavior.m_stateList = [];
            stateBehavior.m_stateSetOverride = cinematic.m_actorStateSet ?? "";
        }

        if (!CoreObjectFactory.FindBehaviorInstance<CinematicActorBehavior>(actor, out var actorBehavior)) {
            Logger.Warning("Cinematic actor template {Id} has no CinematicActorBehavior.", Logger.Args(templateId));

            return null;
        }

        actorBehavior.m_startLocation = start;
        actorBehavior.m_targetLocation = target;
        actorBehavior.m_rootAsset = cinematic.m_rootAsset ?? "";

        return actor;
    }

}
