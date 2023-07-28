/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.Secrets.ServerTypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZoneVolume : WizardZoneObject
{
    private readonly Volume _volume;
    private readonly List<CoreObject> _objsInRadius;

    // ctor
    public WizardZoneVolume(CoreObject activeGameObject, IActorRef wizardZoneRef, Volume volume) 
        : base(activeGameObject, wizardZoneRef)
    {
        this._volume = volume;
        this._objsInRadius = new List<CoreObject>();
    }
    
    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, IActorRef wizardZoneRef, Volume volume)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneVolume(activeGameObject, wizardZoneRef, volume));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY message)
    {
        if (IsInRadius(message.CoreObject))
        {
            // Keep track of the objects already within radius as to not trigger duplicate events.
            if (_objsInRadius.Contains(message.CoreObject))
                return;
            
            // Do enter events.
            _objsInRadius.Add(message.CoreObject);
            foreach (var ev in _volume.m_enterEvents)
            {
                var msg = new ZONE_102_PROTOCOL.MSG_TRIGGER()
                {
                    TriggerName = ev,
                    Suspect = message.Suspect
                };
                WizardZoneRef.Tell(msg);
            }
            
        }
        else if (_objsInRadius.Contains(message.CoreObject) && !IsInRadius(message.CoreObject))
        {
            // Do exit events.
            _objsInRadius.Remove(message.CoreObject);
            foreach (var ev in _volume.m_exitEvents)
            {
                var msg = new ZONE_102_PROTOCOL.MSG_TRIGGER()
                {
                    TriggerName = ev,
                    Suspect = message.Suspect
                };
                WizardZoneRef.Tell(msg);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPLAYER))]
    protected override void ReceiveAddPlayer(ZONE_102_PROTOCOL.MSG_ADDPLAYER message)
    {
        base.ReceiveAddPlayer(message);
        
        if (IsInRadius(message.PlayerObject))
            _objsInRadius.Add(message.PlayerObject);
    }
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER))]
    protected override void ReceiveRemovePlayer(ZONE_102_PROTOCOL.MSG_REMOVEPLAYER message)
    {
        base.ReceiveRemovePlayer(message);
        
        // Remove the player object from our radius to clear up any resources.
        _objsInRadius.RemoveAll(x => x.m_globalID == message.GlobalId);
    }

    private bool IsInRadius(CoreObject obj1)
    {
        var sqrtDist = (obj1.m_location - ActiveGameObject.m_location).LengthSquared();
        var sqrtRadius = _volume.m_radius * _volume.m_radius;

        return sqrtDist <= sqrtRadius;
    }
}