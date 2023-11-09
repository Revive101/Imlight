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
    private const int MovementDelayWithoutMobileId = 300;
    private const int MinimumMovementspeedDelayInMilli = 4000;
    private const int MovementErrorCompensation = 200;

    private readonly CancellationTokenSource _cancelToken;
    private readonly NodeObject[] _nodes;
    private byte _targetNodeIndex;
    private float _movementSpeed = 0.0f;
    private float _movementSpeedMultiplier = 1.0f;
    private DateTime _lastMoveTime;
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
        DoMovementInterval();
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

    protected override bool IsInRadius(CoreObject obj1) {
        // We need to override this method because creatures are moving around.
        var sqrtDist = (obj1.m_location - GetPosition()).LengthSquared();
        var sqrtRadius = InteractionRadius * InteractionRadius;

        return sqrtDist <= sqrtRadius;
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

    private async Task DoMovementInterval() {
        while (!_cancelToken.IsCancellationRequested) {
            // Wait until this object officially has a mobile ID from the zone.
            if (ActiveGameObject.m_nMobileID == 0) {
                await Task.Delay(MovementDelayWithoutMobileId);
                continue;
            }

            // Target a new node.
            _targetNodeIndex = GetNextNodeIndex();

            // Calculate the delay based on the distance between the current position
            // and the target node position, and the movement speed of the creature.
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
            var delay = (int) Math.Round(distance / (travelTimeInSeconds * 1000));

            // Clamp the delay to a minimum.
            if (delay < MinimumMovementspeedDelayInMilli) {
                delay = MinimumMovementspeedDelayInMilli;
            }

            _lastMoveTime = DateTime.Now;
            BroadcastMovement(CurrentTargetNode);

            await Task.Delay(delay);

            // Update the game object position to the target node position once we've reached it.
            UpdateGameObjectPosition(CurrentTargetNode);
        }
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

    private Vector3 GetPosition() {
        // Todo: go back and work on this.

        // If the creature is not moving, then return the current position.
        if (!_isMovingCreature) {
            return ActiveGameObject.m_location;
        }

        // If the creature is moving, then calculate the position based on the
        // current position, the target node position, and the movement speed.
        var pos = ActiveGameObject.m_location;
        var target = CurrentTargetNode.m_location;
        var totalDistance = Vector3.Distance(pos, target);
        var elapsedTimeInSeconds = (DateTime.Now - _lastMoveTime).TotalSeconds;
        var distanceTraveled = elapsedTimeInSeconds * _movementSpeed * _movementSpeedMultiplier;

        if (distanceTraveled >= totalDistance) {
            return target;
        }

        var t = distanceTraveled / totalDistance;
        var x = pos.X + t * (target.X - pos.X) - MovementErrorCompensation;
        var y = pos.Y + t * (target.Y - pos.Y) - MovementErrorCompensation;
        var z = pos.Z + t * (target.Z - pos.Z);

        return new Vector3((float) x, (float) y, (float) z);
    }
}
