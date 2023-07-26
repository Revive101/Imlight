/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using SharpDX;
using WizUnraveler.IO;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.Secrets.ServerTypeCache;

namespace Imlight.Server.Game.Zone;

public class WizardZoneVolume : WizardZoneObject
{
    private readonly Volume _volume;
    private readonly List<CoreObject> _objsInRadius = new List<CoreObject>();

    // ctor
    public WizardZoneVolume(CoreObject activeGameObject, IActorRef wizardZoneRef, Volume volume) 
        : base(activeGameObject, wizardZoneRef)
    {
        this._volume = volume;
    }
    
    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, IActorRef wizardZoneRef, Volume volume)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneVolume(activeGameObject, wizardZoneRef, volume));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_TRIGGERQUERY message)
    {
        var playerPos = new Vector2(message.CoreObject.m_location.X, message.CoreObject.m_location.Z);
        var volPos = new Vector2(ActiveGameObject.m_location.X, ActiveGameObject.m_location.Z);
        var isInRadius = Math.InsideOfCircle(playerPos, _volume.m_radius, volPos);

        if (isInRadius)
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
        else if (_objsInRadius.Contains(message.CoreObject) && !isInRadius)
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
}