/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Serializable.Caches;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using SharpDX;

namespace Imlight.Server.Game.Zone;

/// <summary>
/// An extension of <see cref="WizardZoneObject" /> that adds implementations to move along
/// a given <see cref="WizardZonePath" />.
/// </summary>
public class WizardZonePathCreature : WizardZoneObject
{
    private const float MovementIntervalPerSecond = 0.433f;
    
    private readonly CancellationTokenSource _cancelToken;
    private readonly TypeCache.NodeObject[] _nodes;
    private byte _targetNodeIndex;

    // ctor
    public WizardZonePathCreature(
        TypeCache.CoreObject activeGameObject,
        TypeCache.CoreTemplate template,
        TypeCache.NodeObject[] nodes,
        byte startingNodeIndex,
        IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef)
    {
        _nodes = nodes;
        _cancelToken = new CancellationTokenSource();
        _targetNodeIndex = startingNodeIndex;

#pragma warning disable CS4014
        StartMovementInterval();
#pragma warning restore CS4014
    }

    // Akka.NET ctor
    public static Props Props(
        TypeCache.CoreObject activeGameObject,
        TypeCache.CoreTemplate template,
        TypeCache.NodeObject[] nodes,
        byte startingNodeIndex,
        IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(()
            => new WizardZonePathCreature(activeGameObject, template, nodes, startingNodeIndex, wizardZoneRef));
    }

    /// <summary>
    /// Starts the movement interval for the mob.
    /// </summary>
    private async Task StartMovementInterval()
    {
        // Immediately target the next node.
        _targetNodeIndex = GetNextNodeIndex();

        // Update the move state of the mob, since it's always moving.
        await UpdateMoveState();

        while (!_cancelToken.IsCancellationRequested)
        {
            var delay = (int)Math.Round(1000f / MovementIntervalPerSecond);

            // Wait until this object officially has a mobile ID from the zone.
            if (ActiveGameObject.m_nMobileID == 0)
            {
                await Task.Delay(delay);
                continue;
            }

            await MoveToNextNode();

            await Task.Delay(delay);
        }
    }

    /// <summary>
    /// Updates the move state of the mob.
    /// </summary>
    private async Task UpdateMoveState()
    {
        var moveBroadcast = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST
        {
            Message = new GAME.MSG_MOVESTATE
            {
                GlobalID = ActiveGameObject.m_globalID,
                NewState = 0
            }
        };
        WizardZoneRef.Tell(moveBroadcast);
    }

    /// <summary>
    /// Moves the mob to the next node.
    /// </summary>
    private async Task MoveToNextNode()
    {
        _targetNodeIndex = GetNextNodeIndex();
        var targetNode = _nodes[_targetNodeIndex];

        await BroadcastMovement(targetNode);

        UpdateGameObjectPosition(targetNode);
    }

    /// <summary>
    /// Broadcasts the movement of the mob to the players in the zone.
    /// </summary>
    private async Task BroadcastMovement(TypeCache.NodeObject targetNode)
    {
        var msg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST
        {
            Message = new GAME.MSG_SERVERMOVE
            {
                // Compress fields by a factor of 4.
                Direction = (byte)(targetNode.m_direction / Math.PI / 2 * 250),
                LocationX = (ushort)(targetNode.m_location.X / 4.0f),
                LocationY = (ushort)(targetNode.m_location.Y / 4.0f),
                LocationZ = (ushort)(targetNode.m_location.Z / 4.0f),
                MobileID = ActiveGameObject.m_nMobileID
            }
        };
        WizardZoneRef.Tell(msg);
    }

    /// <summary>
    /// Updates the position of the game object.
    /// </summary>
    private void UpdateGameObjectPosition(TypeCache.NodeObject targetNode)
    {
        ActiveGameObject.m_location = new Vector3(
            targetNode.m_location.X,
            targetNode.m_location.Y,
            targetNode.m_location.Z);
    }
    
    /// <summary>
    /// Calculates the next node, or the first if at end.
    /// </summary>
    /// <returns></returns>
    private byte GetNextNodeIndex()
    {
        if (_targetNodeIndex + 1 >= _nodes.Length)
            return 0;

        return unchecked((byte)(_targetNodeIndex + 1));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    protected override void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
    {
        base.ReceiveAddPlayer(message);

        // Inform the new player that this creature is moving.
        var msgMoveState = new GAME.MSG_MOVESTATE
        {
            GlobalID = ActiveGameObject.m_globalID,
            NewState = 0
        };
        message.Player.Tell(msgMoveState);
    }
}