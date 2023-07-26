/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Game.Zone;

public class WizardZoneVolumeSupervisor : ReceiveProtocolDispatcher
{
    // TODO: Implement supervisor strategy.
    private IActorRef _wizardZoneRef;
    
    // ctor
    public WizardZoneVolumeSupervisor(IActorRef wizardZoneRef)
    {
        this._wizardZoneRef = wizardZoneRef;
    }
    
    // Akka.NET ctor
    public static Props Props(IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneVolumeSupervisor(wizardZoneRef));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDVOLUME))]
    private void ReceiveAddVolume(ZONE_102_PROTOCOL.MSG_ADDVOLUME message)
    {
        // Create an actor representation from the given message.
        var volName = message.Volume.m_volumeName;
        var props = WizardZoneVolume.Props(message.CoreObject, _wizardZoneRef, message.Volume);
        var actorRef = Context.ActorOf(props, volName);
        
        // Respond to the sender with the actor reference we just created.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP { ActorRef = actorRef };
        Sender.Tell(rsp);
    }
}