/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.IO;
using Imlight.Server.Shared.Packets;
using SharpDX;
using WizUnraveler.Cache;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZoneCreature : WizardZoneObject
{
    private const float MovementIntervalPerSecond = 0.433f;

    private NodeObject[] _nodes;
    private IActorRef _zoneRef;
    private byte _targetNodeIndex;
    private CancellationTokenSource _canceltoken;
    
    // ctor
    public WizardZoneCreature(
        CoreObject activeGameObject, 
        NodeObject[] nodes, 
        byte startingNodeIndex,
        IActorRef wizardZoneRef) 
        : base(activeGameObject, wizardZoneRef)
    {
        this._nodes = nodes;
        this._zoneRef = wizardZoneRef;
        this._canceltoken = new CancellationTokenSource();
        this._targetNodeIndex = startingNodeIndex;

        #pragma warning disable CS4014
        StartMovementInterval();
        #pragma warning restore CS4014
    }

    // Akka.NET ctor
    public static Props Props(
        CoreObject activeGameObject, 
        NodeObject[] nodes, 
        byte startingNodeIndex,
        IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() 
            => new WizardZoneCreature(activeGameObject, nodes, startingNodeIndex, wizardZoneRef));
    }

    private async Task StartMovementInterval()
    {
        // Immediately target the next node.
        _targetNodeIndex = GetNextNodeIndex();

        // Update the move state of the mob, since it's always moving.
        var moveBroadcast = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST
        {
            Message = new GAME_5_PROTOCOL.MSG_MOVESTATE
            {
                GlobalID = ActiveGameObject.m_globalID,
                NewState = 0
            }
        };
        _zoneRef.Tell(moveBroadcast);

        while (!_canceltoken.IsCancellationRequested)
        {
            var delay = (int)Math.Round(1000f / MovementIntervalPerSecond);

            // Wait until this object officially has a mobile ID from the zone.
            if (ActiveGameObject.m_nMobileID == 0)
            {
                await Task.Delay(delay);
                continue;
            }
            
            // Select a new target node.
            _targetNodeIndex = GetNextNodeIndex();
            var targetNode = _nodes[_targetNodeIndex];

            // Broadcast the movement of this creature to the players in the zone.
            var msg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST
            {
                Message = new GAME_5_PROTOCOL.MSG_SERVERMOVE
                {
                    // Normalize the vector math (because it's different over DML for.. some.. reason).
                    Direction = (byte)(targetNode.m_direction / Math.PI / 2 * 250),
                    LocationX = (ushort)(targetNode.m_location.X / 4.0f),
                    LocationY = (ushort)(targetNode.m_location.Y / 4.0f),
                    LocationZ = (ushort)(targetNode.m_location.Z / 4.0f),
                    MobileID = ActiveGameObject.m_nMobileID
                }
            };
            _zoneRef.Tell(msg);

            // Update the actual game object position.
            ActiveGameObject.m_location = new Vector3(
                targetNode.m_location.X,
                targetNode.m_location.Y,
                targetNode.m_location.Z);

            await Task.Delay(delay);
        }
    }

    private byte GetNextNodeIndex()
    {
        if (_targetNodeIndex + 1 >= _nodes.Length)
            return 0;

        return unchecked((byte)(_targetNodeIndex + 1));
    }
}