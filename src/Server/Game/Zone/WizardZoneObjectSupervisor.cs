/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Game.Zone;

/// <summary>
/// Supervises a bunch of child <see cref="WizardZoneObject"/> actors.
/// </summary>
public class WizardZoneObjectSupervisor : ReceiveProtocolDispatcher
{
    private readonly IActorRef _wizardZoneRef;
    private readonly List<IActorRef> _objects;

    // ctor
    public WizardZoneObjectSupervisor(IActorRef wizardZoneRef)
    {
        this._wizardZoneRef = wizardZoneRef;
        this._objects = new List<IActorRef>();
    }
    
    // Akka.NET ctor
    public static Props Props(IActorRef wizardZoneRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZoneObjectSupervisor(wizardZoneRef));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
    private void ReceiveAddObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message)
    {
        var props = WizardZoneObject.Props(message.CoreObject, message.Template, _wizardZoneRef);
        CreateActorAndRespond(props);
    }
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPATH))]
    private void ReceiveAddPath(ZONE_102_PROTOCOL.MSG_ADDPATH message)
    {
        var props = WizardZonePath.Props(message.Id, message.Name, message.Nodes, message.Creatures, _wizardZoneRef);
        CreateActorAndRespond(props);
    }
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDVOLUME))]
    private void ReceiveAddVolume(ZONE_102_PROTOCOL.MSG_ADDVOLUME message)
    {
        // Volumes do not use a template, so we pass null.
        var props = WizardZoneVolume.Props(message.CoreObject, null, _wizardZoneRef, message.Volume);
        CreateActorAndRespond(props);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDCREATURE))]
    private void ReceiveAddCreature(ZONE_102_PROTOCOL.MSG_ADDCREATURE message)
    {
        // Creatures are not child actors of this supervisor, but instead are children of the path they belong to.
        _objects.Add(message.ObjectIdentity);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST))]
    private void ReceiveZoneObjectBroadcast(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST message)
    {
        foreach (var obj in _objects)
        {
            foreach (var msg in message.Messages)
            {
                obj.Forward(msg);
            }
        }
    }

    private void CreateActorAndRespond(Props props)
    {
        var actorRef = Context.ActorOf(props);
        _objects.Add(actorRef);
        
        // Respond to the sender with the actor reference we just created.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP { ActorRef = actorRef };
        Sender.Tell(rsp);
    }
}