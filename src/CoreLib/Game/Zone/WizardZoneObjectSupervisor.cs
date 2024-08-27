/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Cryptography;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using static Imlight.Common.Caches.TypeCache;
using Imlight.CoreLib.Game.Zone.ServiceOptions;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// Supervises a bunch of child <see cref="WizardZoneObject"/> actors.
/// </summary>
public class WizardZoneObjectSupervisor : ReceiveProtocolDispatcher {
    private const uint UNIVERSE_TELEPORT_TEMPLATE_ID = 84113;
    private const string DYE_SHOP_GIVEAWAY = "dye";
    private const string AUCTION_HOUSE_GIVEAWAY = "kt-hub-npc14";

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

        var props = DeduceZoneObjectProps(message.CoreObject, message.Template as GameObjectTemplate);
        var actorRef = CreateActorAndRespond(props);
        var npcOptions = GetNpcServiceOptions(message.CoreObject, message.Template as GameObjectTemplate);

        foreach (var option in npcOptions) {
            var msg = new ZONE_102_PROTOCOL.MSG_ADDSERVICEOPTION { ServiceOption = option };
            actorRef.Tell(msg);
        }
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

    private IActorRef CreateActorAndRespond(Props props) {
        var actorRef = Context.ActorOf(props);

        // Send a status check to the object to make sure it loaded correctly.
        var statusCheckMsg = new ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECK();
        var statusCheckRsp = actorRef.
            Ask<ZONE_102_PROTOCOL.MSG_OBJECTSTATUSCHECKRSP>(statusCheckMsg, _statusCheckTimeout)
            .Result;
        if (statusCheckRsp.Failure) {
            Logger.Error("Failed to create zone actor {0} for reason {1}",
                Logger.Args(statusCheckRsp.CoreObject.m_debugName, statusCheckRsp.Error));
            return null;
        }

        // Add the actor to the list of objects we supervise.
        _objects.Add(actorRef, statusCheckRsp.ZoneObject);

        // Respond to the sender with the actor reference we just created.
        var rsp = new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP { ActorRef = actorRef };
        Sender.Tell(rsp);

        return actorRef;
    }

    private static Props DeduceZoneObjectProps(CoreObject obj, GameObjectTemplate template) {
        // Check to see if the object is a world door.
        if (template.m_templateID == UNIVERSE_TELEPORT_TEMPLATE_ID) {
            return WizardZoneNpc.Props(obj, template, null);
        }

        if (template.m_behaviors is not null && template.m_behaviors.Count > 0) {
            // If any behavior is an NPCBehavior, then we know this is an NPC.
            if (template.m_behaviors.Any(x => x is NPCBehaviorTemplate)) {
                return WizardZoneNpc.Props(obj, template, null);
            }
        }

        // If we can't deduce the type, we'll just create a generic zone object.
        return WizardZoneObject.Props(obj, template, null);
    }

    private static List<ServiceOption> GetNpcServiceOptions(CoreObject obj, GameObjectTemplate template) {
        var options = new List<ServiceOption>();
        var npcName = template.m_objectName.ToString().ToLower();

        // Check to see if the object is a teleporter.
        if (template.m_templateID == UNIVERSE_TELEPORT_TEMPLATE_ID) {
            options.Add(new ServiceOptionWorldDoor(obj));
        }

        // Check to see if this NPC has an inventory available on Dragon.
        if (NpcInventoryCollection.TryGetNpcInventory(template.m_templateID, out var inventory)) {
            options.Add(new ServiceOptionVendor(obj, inventory.Inventory));

            var isVendor = WorldVendorLocations.IsVendor(template.m_templateID);
            if (!isVendor) {
                Logger.Verbose("NPC {0} is not a vendor but has an inventory", Logger.Args(template.m_objectName));
            }
        }
        else {
            var isVendor = WorldVendorLocations.IsVendor(template.m_templateID);
            if (isVendor) {
                Logger.Verbose("NPC {0} is a vendor but has no inventory", Logger.Args(template.m_objectName));
            }
        }

        if (NpcSpellInventoryCollection.TryGetNpcInventory(template.m_templateID, out var spellInventory)) {
            options.Add(new ServiceOptionTrain(obj, spellInventory.Spells));
        }

        // Check to see if the NPC is a dye shop.
        if (npcName.Contains(DYE_SHOP_GIVEAWAY)) {
            options.Add(new ServiceOptionDyes(obj));
        }

        // Check to see if the NPC is the auction house vendor.
        if (npcName == AUCTION_HOUSE_GIVEAWAY) {
            options.Add(new ServiceOptionAuction(obj));
        }

        return options;
    }
}
