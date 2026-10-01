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
 * VOLUME COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages spatial trigger volumes in the game world, tracking player 
 * proximity and triggering enter/exit events for specific areas.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Uses actor-based messaging for volume event triggering.
 * Supports dynamic player tracking within defined spatial volumes.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class VolumeComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory {

    // Keyed by the player's actor: a CoreObject is a record, so its hash changes with every move.
    private readonly HashSet<IActorRef> _playersInRange = [];
    private readonly List<(string QuestName, string GoalName)> _volumeGoals = [];
    private Volume _volume;

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        => template is GameObjectTemplate goT && goT.m_templateID == 1700;

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        var isInside = _volume != null && (IsBox ? IsInsideBox(playerObj) : IsInRadius(playerObj, _volume.m_radius));
        if (!isInside || !_playersInRange.Add(playerActor)) {
            return;
        }

        // A player who spawns inside a volume is treated as having walked in. A box volume only
        // drives quest proximity goals (see OnPlayerMove).
        if (IsBox) {
            NotifyProximityGoals(playerObj, playerActor, playerWizard);
        } else {
            OnProximityEnter(playerObj, playerActor, playerWizard, playerSpawned: true);
        }
    }

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) => _playersInRange.Remove(playerActor);

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_volume == null) {
            return;
        }

        // todo: a box volume only drives quest proximity goals here; should it post its enter/exit events too?
        if (IsBox) {
            UpdateBoxProximityGoals(playerObj, playerActor, playerWizard);

            return;
        }

        // Check if the player is now in range of the object.
        if (IsInRadius(playerObj, _volume.m_radius) && !_playersInRange.Contains(playerActor)) {
            // If the player is in range, trigger the enter events.
            OnProximityEnter(playerObj, playerActor, playerWizard);
            _playersInRange.Add(playerActor);
        } else if (!IsInRadius(playerObj, _volume.m_radius) && _playersInRange.Contains(playerActor)) {
            // If the player is out of range, trigger the exit events.
            OnProximityExit(playerObj, playerActor);
            _playersInRange.Remove(playerActor);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_VOLUMEDETAILS))]
    private void ReceiveVolumeDetails(ZONE_102_PROTOCOL.MSG_VOLUMEDETAILS message) {
        _volume = message.Volume;

        // Track every quest goal that references this volume by its proximity tag, so we
        // only ever bother a player's quest service for goals this volume actually activates.
        string volumeName = _volume.m_volumeName;
        if (string.IsNullOrEmpty(volumeName)) {
            return;
        }

        var zonePath = Entity.Zone?.ZonePath;
        foreach (var quest in QuestTemplateCollection.GetAllQuests()) {
            foreach (var goal in quest.m_goals) {
                if (goal is not WaypointGoalTemplate waypointGoal
                    || string.IsNullOrEmpty(waypointGoal.m_proximityTag)
                    || waypointGoal.m_proximityTag != volumeName) {
                    continue;
                }

                // Only track goals that target this zone (when one is specified).
                if (!string.IsNullOrEmpty(waypointGoal.m_zoneTag)
                    && waypointGoal.m_zoneTag != zonePath) {
                    continue;
                }

                _volumeGoals.Add((quest.m_questName, goal.m_goalName));
            }
        }
    }

    private bool IsBox
        => _volume.m_radius <= 0 && _volume.m_length > 0 && _volume.m_width > 0;

    private bool IsInsideBox(CoreObject obj) {
        // Length runs along the volume's local X and width along its local Y, both centered on it.
        var center = Entity.ActiveGameObject.m_location;
        var dx = obj.m_location.X - center.X;
        var dy = obj.m_location.Y - center.Y;
        var yaw = _volume.m_orientation.Z;
        var localX = (dx * MathF.Cos(yaw)) + (dy * MathF.Sin(yaw));
        var localY = (dy * MathF.Cos(yaw)) - (dx * MathF.Sin(yaw));

        return MathF.Abs(localX) <= _volume.m_length / 2 && MathF.Abs(localY) <= _volume.m_width / 2;
    }

    private void UpdateBoxProximityGoals(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (!IsInsideBox(playerObj)) {
            _playersInRange.Remove(playerActor);

            return;
        }

        if (_playersInRange.Add(playerActor)) {
            NotifyProximityGoals(playerObj, playerActor, playerWizard);
        }
    }

    private void OnProximityEnter(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard, bool playerSpawned = false) {
        foreach (var enterEvent in _volume.m_enterEvents) {
            var postEventMsg = new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = enterEvent,
                PlayerActor = playerActor,
                PlayerGameObject = playerObj,
                PlayerSpawned = playerSpawned
            };

            Entity.ZoneRef.Tell(postEventMsg);
        }

        // Quest proximity goals are tied to the volume by name; if the player has one of
        // this volume's goals active, tell their quest service to complete it.
        NotifyProximityGoals(playerObj, playerActor, playerWizard);
    }

    private void NotifyProximityGoals(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_volumeGoals.Count == 0 || playerWizard is null) {
            return;
        }

        foreach (var (questName, goalName) in _volumeGoals) {
            var qInstance = playerWizard.QuestBehavior.CurrentQuestInstances
                .FirstOrDefault(q => q.QuestName == questName);
            if (qInstance is null || !qInstance.IsGoalActive(goalName)) {
                continue;
            }

            var gInstance = qInstance.GoalProgress.FirstOrDefault(g => g.GoalName == goalName);
            if (gInstance is null) {
                continue;
            }

            playerActor.Tell(new ZONE_102_PROTOCOL.MSG_COMPLETEPROXIMITYGOAL {
                QuestID = qInstance.ID,
                GoalID = gInstance.ID,
            });
        }
    }

    private void OnProximityExit(CoreObject playerObj, IActorRef playerActor) {
        foreach (var exitEvent in _volume.m_exitEvents) {
            var postEventMsg = new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = exitEvent,
                PlayerActor = playerActor,
                PlayerGameObject = playerObj
            };

            Entity.ZoneRef.Tell(postEventMsg);
        }
    }

}