/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class VolumeComponent : BaseZoneComponent {

    private readonly Dictionary<CoreObject, IActorRef> _playersInRange = [];
    private Volume _volume;

    public override bool ShouldAttachToEntity(CoreTemplate template) 
        => template.m_behaviors.Any(b => b.m_behaviorName == "BasicProximityBehavior");

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor) {
        // Check if the player is now in range of the object.
        if (IsInRadius(playerObj, _volume.m_radius) && !_playersInRange.ContainsKey(playerObj)) {
            // If the player is in range, trigger the enter events.
            OnProximityEnter(playerObj, playerActor);
            _playersInRange.Add(playerObj, playerActor);
        } else if (!IsInRadius(playerObj, _volume.m_radius) && _playersInRange.ContainsKey(playerObj)) {
            // If the player is out of range, trigger the exit events.
            OnProximityExit(playerObj, playerActor);
            _playersInRange.Remove(playerObj);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDVOLUME))]
    private void ReceiveVolumeDetails(ZONE_102_PROTOCOL.MSG_ADDVOLUME message) {
        _volume = message.Volume;
    }

    private void OnProximityEnter(CoreObject playerObj, IActorRef playerActor) {
        foreach (var enterEvent in _volume.m_enterEvents) {
            var postEventMsg = new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = enterEvent,
                PlayerActor = playerActor,
                PlayerGameObject = playerObj
            };

            Entity.ZoneRef.Tell(postEventMsg);
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