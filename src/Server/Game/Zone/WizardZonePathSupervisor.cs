/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using Akka.Actor;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Game.Zone;

public class WizardZonePathSupervisor : ReceiveProtocolDispatcher
{
    // TODO: Implement supervisor strategy: if a path fails, remove all the mobs and restart the WizardZonePath.
    private IActorRef _wizardZoneRef;
    private readonly List<IActorRef> _paths;
    
    // ctor
    public WizardZonePathSupervisor(IActorRef wizardZoneRef)
    {
        this._wizardZoneRef = wizardZoneRef;
        this._paths = new List<IActorRef>();
    }
    
    // Akka.NET ctor
    public static Props Props(IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZonePathSupervisor(wizardZoneRef));
    }
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPATH))]
    private void ReceiveAddPath(ZONE_102_PROTOCOL.MSG_ADDPATH message)
    {
        // Create a new WizardZonePath actor as a child object of this one.
        var props = WizardZonePath.Props(message.Id, message.Name, message.Nodes, message.Creatures, _wizardZoneRef);
        var actorRef = Context.ActorOf(props);
        _paths.Add(actorRef);
    }
}