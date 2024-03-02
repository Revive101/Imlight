/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// An extension of <see cref="WizardZoneObject" /> that adds implementations to move along
/// a given <see cref="WizardZonePath" />.
/// </summary>
public class WizardZoneCreature : WizardZoneObject {
    internal enum CreatureState {
        Stopped,
        Wandering,
        Combat
    }

    private const int MovementDelayWithoutMobileId = 300;
    private const int MinimumMovementspeedDelayInMilli = 500;
    private const int InteractionIntervalInSeconds = 2;

    private readonly CancellationTokenSource _pathMovementCancelToken;
    private readonly CancellationTokenSource _interactionCancelToken;
    private readonly NodeObject[] _nodes;
    private CreatureState _creatureState;
    private byte _targetNodeIndex;
    private float _movementSpeed = 0.0f;
    private float _movementSpeedMultiplier = 1.0f;
    private DateTime _lastMoveTime;
    private bool _isMovingCreature;
    private bool _isDuelingCreature;

    // ctor
    public WizardZoneCreature(CoreObject activeGameObject,
                              CoreTemplate template,
                              NodeObject[] nodes,
                              byte startingNodeIndex,
                              IActorRef wizardZoneRef)
            : base(activeGameObject, template, wizardZoneRef) {
        this._nodes = nodes;
        this._pathMovementCancelToken = new CancellationTokenSource();
        this._interactionCancelToken = new CancellationTokenSource();
        this._targetNodeIndex = startingNodeIndex;
        this._creatureState = CreatureState.Stopped;

        SetPropertiesFromTemplate();

        if (!this._isMovingCreature) {
            return;
        }

        // Start the movement interval asynchronously as to not block the actor mailbox.
        _ = DoMovementInterval();
        _ = DoSigilInteractionInterval();
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject,
                              CoreTemplate template,
                              NodeObject[] nodes,
                              byte startingNodeIndex,
                              IActorRef wizardZoneRef) {
        return Akka.Actor.Props.Create(()
            => new WizardZoneCreature(activeGameObject, template, nodes, startingNodeIndex, wizardZoneRef));
    }

    protected override void OnPlayerJoin(CoreObject player, IActorRef playerActor) {
        // Since we're not constantly updating the position of the game object, we need to
        // update the position of the game object when a player joins.
        ActiveGameObject.m_location = GetPosition();
        base.OnPlayerJoin(player, playerActor);

        // Inform the new player that this creature is moving.
        // MOVESTATE: 0 = stopped, 1 = moving.
        var msgMoveState = new GAME_5_PROTOCOL.MSG_MOVESTATE {
            GlobalID = ActiveGameObject.m_globalID,
            NewState = (sbyte) (_creatureState == CreatureState.Wandering ? 1 : 0)
        };
        playerActor.Tell(msgMoveState);

        // Inform the new player which node we might be moving to.
        if (_creatureState == CreatureState.Wandering) {
            var msgServerMove = new GAME_5_PROTOCOL.MSG_SERVERMOVE {
                Direction = (byte) (CurrentTargetNode.m_direction / Math.PI / 2 * 250),
                LocationX = (ushort) (CurrentTargetNode.m_location.X / 4.0f),
                LocationY = (ushort) (CurrentTargetNode.m_location.Y / 4.0f),
                LocationZ = (ushort) (CurrentTargetNode.m_location.Z / 4.0f),
                MobileID = ActiveGameObject.m_nMobileID
            };
            playerActor.Tell(msgServerMove);
        }
    }

    protected override void OnPlayerInteractionEnter(CoreObject suspectObject, IActorRef suspectActor) {
        // If I'm a hostile creature and a player just provoked me, then start a duel.
        if (_creatureState == CreatureState.Combat || !_isDuelingCreature) {
            return;
        }

        var msg = new ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL {
            StartingParticipants = new Dictionary<IActorRef, CoreObject>
            {
                { suspectActor, suspectObject },
                { Self, ActiveGameObject }
            }
        };
        WizardZoneRef.Tell(msg);
    }

    protected override Vector3 GetPosition() {
        // If the creature is not moving, then return the current position.
        if (!_isMovingCreature || _creatureState != CreatureState.Wandering) {
            return ActiveGameObject.m_location;
        }

        // If the creature is moving, then calculate the position based on the
        // last node reached, the target node position, and the movement speed.
        var lastNodeReached = ActiveGameObject.m_location;
        var targetNode = CurrentTargetNode.m_location;
        var totalDistance = Vector3.Distance(lastNodeReached, targetNode);
        var elapsedTimeInSeconds = (DateTime.Now - _lastMoveTime).TotalSeconds;
        var distanceTraveled = elapsedTimeInSeconds * _movementSpeed * _movementSpeedMultiplier;

        if (distanceTraveled >= totalDistance) {
            return targetNode;
        }

        var t = distanceTraveled / totalDistance;
        var x = lastNodeReached.X + t * (targetNode.X - lastNodeReached.X);
        var y = lastNodeReached.Y + t * (targetNode.Y - lastNodeReached.Y);
        var z = lastNodeReached.Z + t * (targetNode.Z - lastNodeReached.Z);

        return new Vector3((float) x, (float) y, (float) z);
    }

    [MessageHandler(typeof(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL))]
    private void ReceiveDuelAdd(COMBAT_106_PROTOCOL.MSG_ACTORADDEDTODUEL message) {
        StartCombat();

        ActiveGameObject.m_location = message.SlotPosition;

        // Set the orientation of the creature to the orientation of the slot.
        // The orientation is in radians, so convert it to degrees.
        var orientationInRadians = message.SlotOrientation;
        var orientationInDegrees = orientationInRadians * (180 / Math.PI);
        var orientationVector = new Vector3(0, 0, (float) orientationInDegrees);
        ActiveGameObject.m_orientation = orientationVector;
    }

    private void SetPropertiesFromTemplate() {
        var pathBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is PathMovementBehaviorTemplate) as PathMovementBehaviorTemplate;
        var duelistBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is DuelistBehaviorTemplate) as DuelistBehaviorTemplate;

        if (pathBehavior is not null) {
            this._movementSpeed = pathBehavior.m_movementSpeed;
            this._movementSpeedMultiplier = pathBehavior.m_movementScale;
            this._isMovingCreature = true && pathBehavior.m_movementSpeed > 0.0f;
        }

        if (duelistBehavior is not null) {
            base.InteractionRadius = duelistBehavior.m_npcProximity;
            this._isDuelingCreature = true;
        }

        if (CoreObjectFactory.FindBehaviorInstance<NPCBehavior>(ActiveGameObject, out var behavior)) {
            behavior.m_isMonster = this._isDuelingCreature;
        }
    }

    private async Task DoMovementInterval() {
        _creatureState = CreatureState.Wandering;

        while (!_pathMovementCancelToken.IsCancellationRequested) {
            // Wait until this object officially has a mobile ID from the zone.
            if (ActiveGameObject.m_nMobileID == 0) {
                await Task.Delay(MovementDelayWithoutMobileId);
                continue;
            }

            // Target a new node.
            _targetNodeIndex = GetNextNodeIndex();

            var currentPosition = ActiveGameObject.m_location;
            var distance = Vector3.Distance(currentPosition, CurrentTargetNode.m_location);

            // If the distance is zero, then the mob is already at the target node.
            if (distance == 0) {
                await Task.Delay(MinimumMovementspeedDelayInMilli);
                continue;
            }

            // Calculate the delay based on the distance between the current position
            // and the target node position, and the movement speed of the creature.
            var travelTimeInSeconds = distance / (_movementSpeed * _movementSpeedMultiplier);
            var travelTimeInMilli = (int) travelTimeInSeconds * 1000;

            // Clamp the delay to a minimum.
            if (travelTimeInMilli < MinimumMovementspeedDelayInMilli) {
                travelTimeInMilli = MinimumMovementspeedDelayInMilli;
            }

            // Begin traveling to the target node.
            _lastMoveTime = DateTime.Now;
            BroadcastMovement(CurrentTargetNode);
            await Task.Delay(travelTimeInMilli, _pathMovementCancelToken.Token);

            // Update the game object position to the target node position once we've reached it.
            UpdateGameObjectPosition(CurrentTargetNode);
        }
    }

    private async Task DoSigilInteractionInterval() {
        // todo: fix me
        // On interval, tell the zone to check if this creature is interacting with a sigil object.
        while (!_pathMovementCancelToken.IsCancellationRequested) {
            if (_creatureState == CreatureState.Combat || !_isMovingCreature) {
                break;
            }

            ActiveGameObject.m_location = GetPosition();
            var fishMsg = new ZONE_102_PROTOCOL.MSG_FISHINTERACTION() {
                CoreObject = ActiveGameObject,
                Suspect = Self,
                IsCreature = true
            };
            WizardZoneRef.Tell(fishMsg);

            await Task.Delay(TimeSpan.FromSeconds(InteractionIntervalInSeconds), _interactionCancelToken.Token);
        }
    }

    private void StartCombat() {
        StopMovement();
        _creatureState = CreatureState.Combat;
    }

    private void StopMovement() {
        _pathMovementCancelToken.Cancel();
        _interactionCancelToken.Cancel();
        _creatureState = CreatureState.Stopped;

        // Broadcast the new move state to all players.
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new GAME_5_PROTOCOL.MSG_MOVESTATE {
                GlobalID = ActiveGameObject.m_globalID,
                NewState = 0
            },
            Selfless = true,
            Sender = Self
        };
        WizardZoneRef.Tell(broadcastMsg);
    }

    private byte GetNextNodeIndex() {
        if (_targetNodeIndex + 1 >= _nodes.Length) {
            return 0;
        }

        return unchecked((byte) (_targetNodeIndex + 1));
    }

    private void BroadcastMovement(NodeObject targetNode) {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new GAME_5_PROTOCOL.MSG_SERVERMOVE {
                // Compress fields by a factor of 4.
                Direction = (byte) (targetNode.m_direction / Math.PI / 2 * 250),
                LocationX = (ushort) (targetNode.m_location.X / 4.0f),
                LocationY = (ushort) (targetNode.m_location.Y / 4.0f),
                LocationZ = (ushort) (targetNode.m_location.Z / 4.0f),
                MobileID = ActiveGameObject.m_nMobileID
            }
        };
        WizardZoneRef.Tell(msg);
    }

    private void UpdateGameObjectPosition(NodeObject targetNode) {
        ActiveGameObject.m_location = new Vector3(
            targetNode.m_location.X,
            targetNode.m_location.Y,
            targetNode.m_location.Z);
    }

    private NodeObject CurrentTargetNode => _nodes[_targetNodeIndex];
}
