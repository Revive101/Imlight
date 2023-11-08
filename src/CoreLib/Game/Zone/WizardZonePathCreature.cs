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
using Imlight.CoreLib.Shared.Packets;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// An extension of <see cref="WizardZoneObject" /> that adds implementations to move along
/// a given <see cref="WizardZonePath" />.
/// </summary>
public class WizardZonePathCreature : WizardZoneObject {
    private const int MovementDelayWithoutMobileId = 1000;
    private const int MinimumMovementspeedDelayInMilli = 4000;

    private readonly CancellationTokenSource _cancelToken;
    private readonly NodeObject[] _nodes;
    private byte _targetNodeIndex;
    private float _movementSpeed = 0.0f;
    private float _movementSpeedMultiplier = 1.0f;
    private bool _isMovingCreature;
    private bool _isDuelingCreature;

    // ctor
    public WizardZonePathCreature(
        CoreObject activeGameObject,
        CoreTemplate template,
        NodeObject[] nodes,
        byte startingNodeIndex,
        IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        this._nodes = nodes;
        this._cancelToken = new CancellationTokenSource();
        this._targetNodeIndex = startingNodeIndex;

        SetPropertiesFromTemplate();

        if (!this._isMovingCreature) {
            return;
        }

        // Start the movement interval asynchronously as to not block the actor mailbox.
#pragma warning disable CS4014
        StartMovementInterval();
#pragma warning restore CS4014
    }

    // Akka.NET ctor
    public static Props Props(
        CoreObject activeGameObject,
        CoreTemplate template,
        NodeObject[] nodes,
        byte startingNodeIndex,
        IActorRef wizardZoneRef) {
        return Akka.Actor.Props.Create(()
            => new WizardZonePathCreature(activeGameObject, template, nodes, startingNodeIndex, wizardZoneRef));
    }

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect) {
        base.OnPlayerJoin(player, suspect);

        // Inform the new player that this creature is moving.
        var msgMoveState = new GAME_5_PROTOCOL.MSG_MOVESTATE {
            GlobalID = ActiveGameObject.m_globalID,
            NewState = 0
        };
        suspect.Tell(msgMoveState);
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionEnter(player, suspect);

        if (!_isDuelingCreature) {
            return;
        }

        var msg = new ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL {
            Participants = new Dictionary<IActorRef, CoreObject>
            {
                { suspect, player } ,
                { Self, ActiveGameObject }
            }
        };
        WizardZoneRef.Tell(msg);
    }

    private void SetPropertiesFromTemplate() {
        var pathBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is PathMovementBehaviorTemplate) as PathMovementBehaviorTemplate;
        var duelistBehavior = Template.m_behaviors
            .FirstOrDefault(x => x is DuelistBehaviorTemplate) as DuelistBehaviorTemplate;

        if (pathBehavior is not null) {
            this._movementSpeed = pathBehavior.m_movementSpeed;
            this._movementSpeedMultiplier = pathBehavior.m_movementScale;
            this._isMovingCreature = true;
        }

        if (duelistBehavior is not null) {
            base.InteractionRadius = duelistBehavior.m_npcProximity;
            this._isDuelingCreature = true;
        }
    }

    /// <summary>
    /// Starts the movement interval for the WizardZonePathCreature.
    /// </summary>
    /// <returns>A task that represents the asynchronous movement interval operation.</returns>
    private async Task StartMovementInterval() {
        // Immediately target the next node.
        _targetNodeIndex = GetNextNodeIndex();

        // Update the move state of the mob, since it's always moving.
        // todo: this call may be useless. no players are in the zone yet.
        BroadcastMoveStateChange();

        while (!_cancelToken.IsCancellationRequested) {
            // Wait until this object officially has a mobile ID from the zone.
            if (ActiveGameObject.m_nMobileID == 0) {
                await Task.Delay(MovementDelayWithoutMobileId);
                continue;
            }

            // Calculate the delay based on the distance between the current position
            // and the target node position, and the movement speed of the creature.
            var currentPosition = ActiveGameObject.m_location;
            var targetNodePosition = _nodes[_targetNodeIndex].m_location;
            var distance = Vector3.Distance(currentPosition, targetNodePosition);

            // If the distance is zero, then the mob is already at the target node.
            if (distance == 0) {
                MoveToNextNode();
                await Task.Delay(MinimumMovementspeedDelayInMilli);

                continue;
            }

            var travelTimeInSeconds = distance / (_movementSpeed * _movementSpeedMultiplier);
            var delay = (int) Math.Round(distance / (travelTimeInSeconds * 1000));

            // Clamp the delay to a minimum.
            if (delay < MinimumMovementspeedDelayInMilli) {
                delay = MinimumMovementspeedDelayInMilli;
            }

            MoveToNextNode();

            await Task.Delay(delay);
        }
    }

    /// <summary>
    /// Updates the move state of the mob.
    /// </summary>
    private void BroadcastMoveStateChange() {
        var moveBroadcast = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new GAME_5_PROTOCOL.MSG_MOVESTATE {
                GlobalID = ActiveGameObject.m_globalID,
                NewState = 0
            }
        };
        WizardZoneRef.Tell(moveBroadcast);
    }

    /// <summary>
    /// Moves the mob to the next node.
    /// </summary>
    private void MoveToNextNode() {
        _targetNodeIndex = GetNextNodeIndex();
        var targetNode = _nodes[_targetNodeIndex];

        BroadcastMovement(targetNode);
        UpdateGameObjectPosition(targetNode);
    }

    /// <summary>
    /// Broadcasts the movement of the mob to the players in the zone.
    /// </summary>
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

    /// <summary>
    /// Updates the position of the game object.
    /// </summary>
    private void UpdateGameObjectPosition(NodeObject targetNode) {
        ActiveGameObject.m_location = new Vector3(
            targetNode.m_location.X,
            targetNode.m_location.Y,
            targetNode.m_location.Z);
    }

    /// <summary>
    /// Calculates the next node, or the first if at end.
    /// </summary>
    /// <returns></returns>
    private byte GetNextNodeIndex() {
        if (_targetNodeIndex + 1 >= _nodes.Length) {
            return 0;
        }

        return unchecked((byte) (_targetNodeIndex + 1));
    }
}
