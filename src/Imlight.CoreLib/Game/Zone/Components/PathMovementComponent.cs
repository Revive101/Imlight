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
 * PATH MOVEMENT COMPONENT 
 * ========================================================================
 * 
 * PURPOSE:
 * Manages dynamic movement mechanics for entities with predefined path behaviors, 
 * supporting various path types and movement patterns.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Supports multiple path traversal types (chain, loop, random). An entity whose path details never
 * arrive stops moving and stays in place.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Math;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class PathMovementComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IComponentFactory, IWithTimers {

    private const string CREATURE_SPAWN_INTERVAL_LOCK = "CREATURE_SPAWN_INTERVAL_LOCK";
    private const string CREATURE_FISH_INTERVAL_LOCK = "CREATURE_FISH_INTERVAL_LOCK";
    private const string CREATURE_TRAVEL_UPDATE_INTERVAL_LOCK = "CREATURE_TRAVEL_UPDATE_INTERVAL_LOCK";
    private const uint INITIAL_MOVEMENT_DELAY_MINIMUM_IN_MS = 1000;
    private const uint INITIAL_MOVEMENT_DELAY_MAXIMUM_IN_MS = 3000;
    private const uint TRAVEL_TIME_CLAMP_MINIMUM_IN_MS = 1000;
    private const uint CREATURE_FISH_INTERVAL_IN_MS = 250;
    private const uint PATH_DETAILS_FAILURE_COUNT_MAXIMUM = 5;
    private const uint TRAVEL_UPDATE_INTERVAL_IN_MS = 380;
    private const sbyte MOVE_STATE_IDLE = 0;
    private const sbyte MOVE_STATE_TRAVELLING = 1;
    private const float DIRECTION_UNITS_PER_RADIAN = 40.0f;
    private const int DIRECTION_UNITS_MAXIMUM = 251;
    private const float COORDINATE_UNITS_PER_WIRE_UNIT = 4.0f;

    public bool Stopped { get; set; }
    public ITimerScheduler Timers { get; set; }

    private PathType _pathType;
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
    private Vector3 _lastStartLocation;
    private bool _isTravelling;
    private byte _segmentDirection;
    // todo: do actions

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_behaviors.Any(x => x is PathMovementBehaviorTemplate pathMovement
            && pathMovement.m_movementSpeed > 0.0f)
        && goTemplate.m_behaviors.Any(x => x is PathBehaviorTemplate);

    public override void OnStart() {
        // A summoned combat minion stays in its duel slot and must not path around the world.
        if (Entity.IsCombatOnlyMinion) {
            return;
        }

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
        var fishInteractionInterval = TimeSpan.FromMilliseconds(CREATURE_FISH_INTERVAL_IN_MS);
        var fishInteractionMsg = new ZONE_102_PROTOCOL.MSG_CREATUREFISHINTERACTIONINTERVAL();
        Timers.StartPeriodicTimer(CREATURE_FISH_INTERVAL_LOCK, fishInteractionMsg, fishInteractionInterval);
    }

    /// <summary>
    /// Tells a player who was just sent this creature's MSG_NEWOBJECT where it is and whether it is travelling.
    /// </summary>
    public void SendMovementTo(IActorRef playerActor) {
        // The client drops a move for an object it does not have yet, so this must follow the object itself.
        if (_currentNode is null) {
            return;
        }

        playerActor.Tell(CreateMoveStateMessage(_isTravelling ? MOVE_STATE_TRAVELLING : MOVE_STATE_IDLE));
        playerActor.Tell(CreateServerMoveMessage(GetPosition()));
    }

    /// <summary>
    /// Tells every player in the zone, just sent this creature's MSG_NEWOBJECT, where it is and whether it is travelling.
    /// </summary>
    public void BroadcastMovement() {
        if (_currentNode is null) {
            return;
        }

        BroadcastMoveState(_isTravelling ? MOVE_STATE_TRAVELLING : MOVE_STATE_IDLE);
        BroadcastServerMove(GetPosition());
    }

    public void Stop() {
        Stopped = true;
        if (_isTravelling) {
            Entity.ActiveGameObject.m_location = GetPosition();
        }

        _isTravelling = false;
        Timers.Cancel(CREATURE_TRAVEL_UPDATE_INTERVAL_LOCK);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PATHDETAILS))]
    private void ReceivePathDetails(ZONE_102_PROTOCOL.MSG_PATHDETAILS message) {
        _receivedPathDetails = true;

        // Ensure that the nodes are ordered as per their ID
        _nodes = [.. message.NodeObjects.OrderBy(node => Convert.ToUInt32(node.m_id))];

        // The ZonePath spawned us at one of the nodes, and our location is currently set to it.
        // Find the node that has the same location as us and set it as the current node.
        _currentNode = _nodes.FirstOrDefault(node => node.m_location == Entity.ActiveGameObject.m_location);
        _lastStartLocation = Entity.ActiveGameObject.m_location;
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

        if (_nodes.Count < 2) {
            return;
        }

        if (Stopped) {
            RestartMoveInterval(INITIAL_MOVEMENT_DELAY_MAXIMUM_IN_MS);

            return;
        }

        var arrived = _isTravelling;
        if (arrived) {
            ArriveAtNode();
        }

        // Creatures have a chance to pause at each node. This stops them from clumping together.
        // If the creature is already paused, then don't pause again.
        if (ShouldPause() && !_justPaused) {
            _justPaused = true;
            PauseAtNode(arrived);
            RestartMoveInterval(_pauseDuration * 1000);

            return;
        }
        _justPaused = false;

        StartSegment(arrived);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATURETRAVELUPDATEINTERVAL))]
    private void ReceiveTravelUpdateInterval() {
        if (Stopped || !_isTravelling) {
            Timers.Cancel(CREATURE_TRAVEL_UPDATE_INTERVAL_LOCK);

            return;
        }

        BroadcastServerMove(GetPosition());
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

    private void ArriveAtNode() {
        _isTravelling = false;
        Timers.Cancel(CREATURE_TRAVEL_UPDATE_INTERVAL_LOCK);
        Entity.ActiveGameObject.m_location = _currentNode.m_location;
    }

    private void PauseAtNode(bool arrived) {
        if (!arrived) {
            return;
        }

        // todo: confirm, live showed no pauses
        BroadcastServerMove(_currentNode.m_location);
        BroadcastMoveState(MOVE_STATE_IDLE);
    }

    private void StartSegment(bool arrived) {
        // At a node the client gets the arrival with the old direction, then the next segment's first update.
        BroadcastMoveState(MOVE_STATE_TRAVELLING);
        if (arrived) {
            BroadcastServerMove(_currentNode.m_location);
        }

        _lastStartLocation = Entity.ActiveGameObject.m_location;
        _currentNode = GetNextNode();
        _segmentDirection = EncodeDirection(_currentNode.m_location - _lastStartLocation, _segmentDirection);

        // Determine how long it will take to reach the new node.
        var distanceToNewNode = Vector3.Distance(_lastStartLocation, _currentNode.m_location);
        var travelTimeInSeconds = distanceToNewNode / (_movementSpeed * _movementScale);
        var travelTimeInMilli = (uint) (travelTimeInSeconds * 1000);

        if (travelTimeInMilli < TRAVEL_TIME_CLAMP_MINIMUM_IN_MS) {
            travelTimeInMilli = TRAVEL_TIME_CLAMP_MINIMUM_IN_MS;
        }

        _lastMoveTime = DateTime.Now;
        _isTravelling = true;
        BroadcastServerMove(_lastStartLocation);
        RestartMoveInterval(travelTimeInMilli);

        var travelUpdateInterval = TimeSpan.FromMilliseconds(TRAVEL_UPDATE_INTERVAL_IN_MS);
        var travelUpdateMsg = new ZONE_102_PROTOCOL.MSG_CREATURETRAVELUPDATEINTERVAL();
        Timers.StartPeriodicTimer(CREATURE_TRAVEL_UPDATE_INTERVAL_LOCK, travelUpdateMsg, travelUpdateInterval);
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
        if (_pathType == PathType.PT_CHAIN) {
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
        if (_pathType == PathType.PT_LOOP) {
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
        if (_pathType == PathType.PT_RANDOM) {
            var randomIndex = new Random().Next(0, _nodes.Count);

            return _nodes[randomIndex];
        }

        throw new NotImplementedException($"Path type {_pathType} is not implemented.");
    }

    private void BroadcastMoveState(sbyte newState) => PlayerBroadcast(CreateMoveStateMessage(newState));

    private void BroadcastServerMove(Vector3 location) => PlayerBroadcast(CreateServerMoveMessage(location));

    private GAME_5_PROTOCOL.MSG_MOVESTATE CreateMoveStateMessage(sbyte newState)
        => new() {
            GlobalID = Entity.ActiveGameObject.m_globalID,
            NewState = newState
        };

    private GAME_5_PROTOCOL.MSG_SERVERMOVE CreateServerMoveMessage(Vector3 location)
        => new() {
            Direction = _segmentDirection,
            LocationX = EncodeCoordinate(location.X),
            LocationY = EncodeCoordinate(location.Y),
            LocationZ = EncodeCoordinate(location.Z),
            MobileID = Entity.MobileID
        };

    private static byte EncodeDirection(Vector3 travelDelta, byte stationaryDirection) {
        if (travelDelta.X == 0 && travelDelta.Y == 0) {
            return stationaryDirection;
        }

        var radians = (MathF.Atan2(-travelDelta.X, -travelDelta.Y) + MathF.Tau) % MathF.Tau;
        var units = (int) MathF.Round(radians * DIRECTION_UNITS_PER_RADIAN);

        return (byte) Math.Min(units, DIRECTION_UNITS_MAXIMUM);
    }

    private static ushort EncodeCoordinate(float coordinate)
        => unchecked((ushort) (short) MathF.Round(coordinate / COORDINATE_UNITS_PER_WIRE_UNIT));

    private Vector3 GetPosition() {
        if (_currentNode is null) {
            return Entity.ActiveGameObject.m_location;
        }

        var startLocation = _lastStartLocation;
        var targetNode = _currentNode.m_location;
        var totalDistance = Vector3.Distance(startLocation, targetNode);

        // Guard against zero-distance (creature is stationary — spawned, paused, or arrived).
        if (totalDistance < 0.001f) {
            return targetNode;
        }

        var elapsedTimeInMs = (DateTime.Now - _lastMoveTime).TotalMilliseconds;
        var distanceTraveled = _movementSpeed * _movementScale * elapsedTimeInMs / 1000;

        // Clamp t between 0 and 1 to prevent overshooting.
        var t = Math.Clamp(distanceTraveled / totalDistance, 0, 1);

        var x = startLocation.X + t * (targetNode.X - startLocation.X);
        var y = startLocation.Y + t * (targetNode.Y - startLocation.Y);
        var z = startLocation.Z + t * (targetNode.Z - startLocation.Z);

        return new Vector3((float) x, (float) y, (float) z);
    }

    private bool CheckPathDetails() {
        if (!_receivedPathDetails) {
            _pathDetailsFailureCount++;

            if (_pathDetailsFailureCount > PATH_DETAILS_FAILURE_COUNT_MAXIMUM) {
                Logger.Warning(
                    "Creature {0} in zone {1} has not received path details after {2} seconds, so it stays where it stands.",
                    Logger.Args(
                        Entity.ActiveGameObject.m_debugName,
                        Entity.Zone.ZoneName,
                        PATH_DETAILS_FAILURE_COUNT_MAXIMUM * INITIAL_MOVEMENT_DELAY_MINIMUM_IN_MS / 1000
                    )
                );

                // An object placed in the zone rather than spawned by a path never gets path details. It keeps
                // standing instead of being deleted, which also left its movement timers firing at a dead actor.
                Stop();
                Timers.CancelAll();

                return false;
            }

            RestartMoveInterval(INITIAL_MOVEMENT_DELAY_MINIMUM_IN_MS);

            return false;
        }

        return true;
    }

}