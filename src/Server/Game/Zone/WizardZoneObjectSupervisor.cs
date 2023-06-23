/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Game.Zone;

/// <summary>
/// Supervises a bunch of child <see cref="WizardZoneObject"/> actors.
/// </summary>
public class WizardZoneObjectSupervisor : ReceiveProtocolDispatcher
{
    // TODO: Implement supervisor strategy: if a path fails, remove all the mobs and restart the WizardZonePath.
    private IActorRef _wizardZoneRef;
    
    // ctor
    public WizardZoneObjectSupervisor(IActorRef wizardZoneRef)
    {
        this._wizardZoneRef = wizardZoneRef;
    }
    
    // Akka.NET ctor
    public static Props Props(IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneObjectSupervisor(wizardZoneRef));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
    private void ReceiveAddObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message)
    {
        // Create the object as a child actor of this supervisor.
        var props = WizardZoneObject.Props(message.CoreObject, _wizardZoneRef);
        var actorRef = Context.ActorOf(props);
        
        // Formulate response. Don't worry about not giving the mobile id; the WizardZone handles that part.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP { ActorRef = actorRef };
        Sender.Tell(rsp);
    }
}