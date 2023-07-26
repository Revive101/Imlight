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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ASKFORINTERACTION))]
    private void ReceiveZoneInteraction(ZONE_102_PROTOCOL.MSG_ASKFORINTERACTION message)
    {
        var playerPos = new Vector2(message.CoreObject.m_location.X, message.CoreObject.m_location.Z);
        var volPos = new Vector2(ActiveGameObject.m_location.X, ActiveGameObject.m_location.Z);
        var isInRadius = Math.InsideOfCircle(playerPos, _volume.m_radius, volPos);

        if (isInRadius)
        {
            
        }
    }
}