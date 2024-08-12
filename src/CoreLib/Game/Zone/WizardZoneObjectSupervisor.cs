/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Cryptography;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// Supervises a bunch of child <see cref="WizardZoneObject"/> actors.
/// </summary>
public class WizardZoneObjectSupervisor : ReceiveProtocolDispatcher {
    private const uint UNIVERSE_TELEPORT_TEMPLATE_ID = 84113;

    private readonly IActorRef _wizardZoneRef;
    private readonly Dictionary<IActorRef, WizardZoneObject> _objects;
    private readonly TimeSpan _statusCheckTimeout = TimeSpan.FromSeconds(5);

    // ctor
    public WizardZoneObjectSupervisor(IActorRef wizardZoneRef) {
        this._wizardZoneRef = wizardZoneRef;
        this._objects = new Dictionary<IActorRef, WizardZoneObject>();
    }

    // Akka.NET ctor
    public static Props Props(IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneObjectSupervisor(wizardZoneRef));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDOBJECT))]
    private void ReceiveAddObject(ZONE_102_PROTOCOL.MSG_ADDOBJECT message) {
        if (message.Template is null) {
            Logger.Error("Received a null template for object {0}", Logger.Args(message.CoreObject.m_debugName));
            return;
        }

        var props = message.Template.m_behaviors != null
            ? DeduceObjectType(message.CoreObject, message.Template as GameObjectTemplate)
            : WizardZoneObject.Props(message.CoreObject, message.Template, _wizardZoneRef);

        CreateActorAndRespond(props);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDPATH))]
    private void ReceiveAddPath(ZONE_102_PROTOCOL.MSG_ADDPATH message) {
        // The path type does not inherit from WizardZoneObject. It doesn't need a status check.
        var props = WizardZonePath.Props(message.Id, message.Name, message.Nodes, message.Creatures, _wizardZoneRef);
        var actorRef = Context.ActorOf(props);

        // Respond to the sender with the actor reference we just created.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP { ActorRef = actorRef };
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDVOLUME))]
    private void ReceiveAddVolume(ZONE_102_PROTOCOL.MSG_ADDVOLUME message) {
        // Volumes do not use a template, so we pass null.
        var props = WizardZoneVolume.Props(message.CoreObject,
                                           null,
                                           _wizardZoneRef,
                                           message.Volume);
        CreateActorAndRespond(props);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDCREATURE))]
    private void ReceiveAddCreature(ZONE_102_PROTOCOL.MSG_ADDCREATURE message) {
        try {
            var statusCheckMsg = new ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECK();
            var statusCheckRsp = message.ObjectIdentity
                .Ask<ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECKRSP>(statusCheckMsg, _statusCheckTimeout)
                .Result;

            // Creatures are not child actors of this supervisor, but instead are children of the path they belong to.
            _objects.Add(message.ObjectIdentity, statusCheckRsp.ZoneObject);
        }
        catch  { }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST))]
    private void ReceiveZoneObjectBroadcast(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST message) {
        foreach (var obj in _objects) {
            foreach (var msg in message.Messages) {
                obj.Key.Forward(msg);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT))]
    private void ReceiveQueryObject(ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT message) {
        var obj = _objects.Values.First(x =>
               x.ActiveGameObject.m_globalID == message.GlobalID
            || x.ActiveGameObject.m_nMobileID == message.MobileID);
        var rsp = new ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTRSP { ZoneObject = obj };

        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD))]
    private void ReceiveAddDynaMod(CHARACTER_103_PROTOCOL.MSG_ADDDYNAMOD message) {
        foreach (var obj in _objects) {
            if (obj.Value.ActiveGameObject.m_zoneTagID != StringHash.Compute(message.DynaMod.m_dynaModClientTag)) {
                continue;
            }

            obj.Key.Tell(message);
        }
    }

    private void CreateActorAndRespond(Props props) {
        var actorRef = Context.ActorOf(props);

        // Send a status check to the object to make sure it loaded correctly.
        var statusCheckMsg = new ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECK();
        var statusCheckRsp = actorRef.
            Ask<ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECKRSP>(statusCheckMsg, _statusCheckTimeout)
            .Result;
        if (statusCheckRsp.Failure) {
            Logger.Error("Failed to create zone actor {0} for reason {1}",
                Logger.Args(statusCheckRsp.CoreObject.m_debugName, statusCheckRsp.Error));
            return;
        }

        // Add the actor to the list of objects we supervise.
        _objects.Add(actorRef, statusCheckRsp.ZoneObject);

        // Respond to the sender with the actor reference we just created.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP { ActorRef = actorRef };
        Sender.Tell(rsp);
    }

    private static Props DeduceObjectType(CoreObject obj, GameObjectTemplate template) {
        var objBehaviors = template.m_behaviors;

        // Check to see if any of the behaviors are of type NPCBehavior.
        if (objBehaviors != null) {
            foreach (var behavior in objBehaviors) {
                if (behavior is NPCBehaviorTemplate) {
                    return WizardZoneNpc.Props(obj, template, Context.Self);
                }
            }
        }

        if (template.m_templateID == UNIVERSE_TELEPORT_TEMPLATE_ID) {
            return WizardZoneTeleportDoor.Props(obj, template, Context.Self);
        }

        return WizardZoneObject.Props(obj, template, Context.Self);
    }
}
