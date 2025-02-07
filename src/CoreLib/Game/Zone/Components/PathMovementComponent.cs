/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class PathMovementComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory, IWithTimers {

    private const string CREATURE_SPAWN_INTERVAL_LOCK = "CREATURE_SPAWN_INTERVAL_LOCK";
    private const string CREATURE_FISH_INTERVAL_LOCK = "CREATURE_FISH_INTERVAL_LOCK";
    private const uint INITIAL_MOVEMENT_DELAY_MINIMUM_IN_MS = 1000;
    private const uint INITIAL_MOVEMENT_DELAY_MAXIMUM_IN_MS = 3000;
    private const uint TRAVEL_TIME_CLAMP_MINIMUM_IN_MS = 1000;
    private const uint PATH_DETAILS_FAILURE_COUNT_MAXIMUM = 5;

    public bool Stopped { get; set; }
    public ITimerScheduler Timers { get; set; }

    private PathBehaviorTemplate.PathType _pathType;
    private int _pathDirection;
    private uint _pauseChance;
    private float _pauseDuration;
    private float _movementSpeed;
    private float _movementScale;
    private bool _justPaused;
    private List<NodeObject> _nodes;
    private NodeObject _currentNode;
    private int _currentChainDirection;
    private bool _receivedPathDetails;
    private uint _pathDetailsFailureCount;
    private DateTime _lastMoveTime;
    // todo: do actions

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_behaviors.Any(x => x is PathMovementBehaviorTemplate pathMovement
            && pathMovement.m_movementSpeed > 0.0f)
        && goTemplate.m_behaviors.Any(x => x is PathBehaviorTemplate);

    public override void OnStart() {
        var pathBehavior = Entity.Template.m_behaviors.OfType<PathBehaviorTemplate>().First();
        var pathMovementBehavior = Entity.Template.m_behaviors.OfType<PathMovementBehaviorTemplate>().First();

        this._pathType = pathBehavior.m_kPathType;
        this._pathDirection = pathBehavior.m_nPathDirection;
        this._pauseChance = pathBehavior.m_pauseChance;
        this._pauseDuration = pathBehavior.m_timeToPause;
        this._movementSpeed = pathMovementBehavior.m_movementSpeed;
        this._movementScale = pathMovementBehavior.m_movementScale;
        this._currentChainDirection = pathBehavior.m_nPathDirection;

        // Begin the creature spawn interval. Randomize the initial delay.
        // Remain stopped until the ZonePath can inform us of the path details.
        var randomDelay = new Random().Next(
                (int) INITIAL_MOVEMENT_DELAY_MINIMUM_IN_MS,
                (int) INITIAL_MOVEMENT_DELAY_MAXIMUM_IN_MS
        );
        RestartMoveInterval(randomDelay);

        // Begin the creature fish interaction interval.
        var fishInteractionInterval = TimeSpan.FromMilliseconds(TRAVEL_TIME_CLAMP_MINIMUM_IN_MS);
        var fishInteractionMsg = new ZONE_102_PROTOCOL.MSG_CREATUREFISHINTERACTIONINTERVAL();
        Timers.StartPeriodicTimer(CREATURE_FISH_INTERVAL_LOCK, fishInteractionMsg, fishInteractionInterval);
    }

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (_currentNode is null) {
            return;
        }

        // Inform the player of where the creature is moving to.
        var movestateMsg = new GAME_5_PROTOCOL.MSG_MOVESTATE {
            GlobalID = Entity.ActiveGameObject.m_globalID,
            NewState = 0
        };
        playerActor.Tell(movestateMsg);

        var movementMsg = new GAME_5_PROTOCOL.MSG_SERVERMOVE {
            Direction = (byte) (_currentNode.m_direction / Math.PI / 2 * 250),
            LocationX = (ushort) (_currentNode.m_location.X / 4.0f),
            LocationY = (ushort) (_currentNode.m_location.Y / 4.0f),
            LocationZ = (ushort) (_currentNode.m_location.Z / 4.0f),
            MobileID = Entity.MobileID
        };
        playerActor.Tell(movementMsg);
    }

    public void Stop() => Stopped = true;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PATHDETAILS))]
    private void ReceivePathDetails(ZONE_102_PROTOCOL.MSG_PATHDETAILS message) {
        _receivedPathDetails = true;

        // Ensure that the nodes are ordered as per their ID
        _nodes = [.. message.NodeObjects.OrderBy(node => Convert.ToUInt32(node.m_id))];

        // The ZonePath spawned us at one of the nodes, and our location is currently set to it.
        // Find the node that has the same location as us and set it as the current node.
        _currentNode = _nodes.FirstOrDefault(node => node.m_location == Entity.ActiveGameObject.m_location);
        if (_currentNode is null) {
            Logger.Error(
                "Creature {0} in zone {1} was spawned at an unknown location.",
                Logger.Args(Entity.ActiveGameObject.m_debugName, Entity.Zone.ZoneName)
            );
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL))]
    private void ReceiveMoveInterval(ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL message) {
        if (!CheckPathDetails()) {
            return;
        }

        if (Stopped) {
            RestartMoveInterval(INITIAL_MOVEMENT_DELAY_MAXIMUM_IN_MS);

            return;
        }

        // Creatures have a chance to pause at each node. This stops them from clumping together.
        // If the creature is already paused, then don't pause again.
        if (ShouldPause() && !_justPaused) {
            _justPaused = true;
            RestartMoveInterval(_pauseDuration);

            return;
        }
        _justPaused = false;

        // Target a new node and begin moving towards it.
        _currentNode = GetNextNode();

        // Determine how long it will take to reach the new node.
        var distanceToNewNode = Vector3.Distance(Entity.ActiveGameObject.m_location, _currentNode.m_location);
        var travelTimeInSeconds = distanceToNewNode / (_movementSpeed * _movementScale);
        var travelTimeInMilli = (uint) travelTimeInSeconds * 1000;

        if (travelTimeInMilli < TRAVEL_TIME_CLAMP_MINIMUM_IN_MS) {
            travelTimeInMilli = TRAVEL_TIME_CLAMP_MINIMUM_IN_MS;
        }

        UpdateGameObjectLocation(_currentNode);
        BroadcastMovement(_currentNode);
        RestartMoveInterval(travelTimeInMilli);
        _lastMoveTime = DateTime.Now;
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATUREFISHINTERACTIONINTERVAL))]
    private void ReceiveCreatureFishInteractionInterval() {
        if (Stopped) {
            return;
        }

        Entity.ActiveGameObject.m_location = GetPosition();

        var msg = new ZONE_102_PROTOCOL.MSG_CREATUREMOVE() {
            CreatureActor = Entity.SelfRef,
            CreatureObject = Entity.ActiveGameObject,
            CreatureEntity = Entity
        };
        Entity.ZoneRef.Tell(msg);
    }

    private bool ShouldPause() {
        if (_pauseChance == 0) {
            return false;
        }

        return new Random().Next(0, 100) < _pauseChance;
    }

    private void RestartMoveInterval(float delayTimeInMs) {
        var delay = TimeSpan.FromMilliseconds(delayTimeInMs);
        var msg = new ZONE_102_PROTOCOL.MSG_CREATUREMOVEINTERVAL();
        Timers.StartSingleTimer(CREATURE_SPAWN_INTERVAL_LOCK, msg, delay);
    }

    private NodeObject GetNextNode() {
        if (_currentNode is null) {
            return _nodes.First();
        }

        // If the path type is chain, we move node1 -> node2 -> node3 -> node2 -> node1.
        if (_pathType == PathBehaviorTemplate.PathType.PT_CHAIN) {
            var currentIndex = _nodes.IndexOf(_currentNode);
            var nextIndex = currentIndex + _currentChainDirection;

            // If the next index is out of bounds, then we reverse the direction.
            if (nextIndex < 0 || nextIndex >= _nodes.Count) {
                _currentChainDirection = -_currentChainDirection;
                nextIndex = currentIndex + _currentChainDirection;
            }

            return _nodes[nextIndex];
        }

        // If the path type is loop, we move node1 -> node2 -> node3 -> node1 or reversed based on _pathDirection.
        if (_pathType == PathBehaviorTemplate.PathType.PT_LOOP) {
            var currentIndex = _nodes.IndexOf(_currentNode);
            var nextIndex = _pathDirection == 0 ? currentIndex + 1 : currentIndex - 1;

            // If the next index is out of bounds, then we loop back to the start or end based on _pathDirection.
            if (nextIndex >= _nodes.Count) {
                nextIndex = 0;
            }
            else if (nextIndex < 0) {
                nextIndex = _nodes.Count - 1;
            }

            return _nodes[nextIndex];
        }

        // If the path type is random, we move to a random node.
        if (_pathType == PathBehaviorTemplate.PathType.PT_RANDOM) {
            var randomIndex = new Random().Next(0, _nodes.Count);

            return _nodes[randomIndex];
        }

        throw new NotImplementedException($"Path type {_pathType} is not implemented.");
    }

    private void BroadcastMovement(NodeObject nodeObject) {
        // Send the move state message to the zone.
        var movestateMsg = new GAME_5_PROTOCOL.MSG_MOVESTATE {
            GlobalID = Entity.ActiveGameObject.m_globalID,
            NewState = 0
        };
        var movestateMsgBroadcast = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = movestateMsg
        };
        Entity.ZoneRef.Tell(movestateMsgBroadcast);

        // Send the actual movement message to the zone.
        // The direction is in radians and the coordinates are compressed by a factor of 4.
        var movementMsg = new GAME_5_PROTOCOL.MSG_SERVERMOVE {
            Direction = (byte) (nodeObject.m_direction / Math.PI / 2 * 250),
            LocationX = (ushort) (nodeObject.m_location.X / 4.0f),
            LocationY = (ushort) (nodeObject.m_location.Y / 4.0f),
            LocationZ = (ushort) (nodeObject.m_location.Z / 4.0f),
            MobileID = Entity.MobileID
        };
        var movementMsgBroadcast = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = movementMsg
        };
        Entity.ZoneRef.Tell(movementMsgBroadcast);
    }

    private void UpdateGameObjectLocation(NodeObject nodeObject)
        => Entity.ActiveGameObject.m_location = new Vector3(
            nodeObject.m_location.X,
            nodeObject.m_location.Y,
            nodeObject.m_location.Z
        );

    private Vector3 GetPosition() {
        if (_currentNode is null) {
            return Entity.ActiveGameObject.m_location;
        }

        var lastLocation = Entity.ActiveGameObject.m_location;
        var targetNode = _currentNode.m_location;
        var totalDistance = Vector3.Distance(lastLocation, targetNode);
        var elapsedTimeInMsg = (DateTime.Now - _lastMoveTime).TotalMilliseconds;
        var distanceTraveled = _movementSpeed * _movementScale * elapsedTimeInMsg / 1000;

        // Clamp t between 0 and 1 to prevent overshooting.
        var t = Math.Clamp(distanceTraveled / totalDistance, 0, 1);

        var x = lastLocation.X + t * (targetNode.X - lastLocation.X);
        var y = lastLocation.Y + t * (targetNode.Y - lastLocation.Y);
        var z = lastLocation.Z + t * (targetNode.Z - lastLocation.Z);

        return new Vector3((float) x, (float) y, (float) z);
    }

    private bool CheckPathDetails() {
        if (!_receivedPathDetails) {
            _pathDetailsFailureCount++;

            if (_pathDetailsFailureCount > PATH_DETAILS_FAILURE_COUNT_MAXIMUM) {
                Logger.Error(
                    "Creature {0} in zone {1} tried moving but still has not received path details after {2} seconds.",
                    Logger.Args(
                        Entity.ActiveGameObject.m_debugName,
                        Entity.Zone.ZoneName,
                        PATH_DETAILS_FAILURE_COUNT_MAXIMUM * INITIAL_MOVEMENT_DELAY_MINIMUM_IN_MS / 1000
                    )
                );

                return false;
            }

            RestartMoveInterval(INITIAL_MOVEMENT_DELAY_MINIMUM_IN_MS);

            return false;
        }

        return true;
    }

}