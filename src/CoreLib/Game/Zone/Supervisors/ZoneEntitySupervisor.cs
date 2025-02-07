/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Nito.AsyncEx.Synchronous;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor
/// for any entities that are created within the zone.
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
internal abstract class ZoneEntitySupervisor(Core.Zone zone) : ReceiveProtocolDispatcher {

    protected const uint OBJECT_CREATION_TIMEOUT_IN_MS = 5000;

    protected readonly IActorRef ZoneRef = Context.Parent;
    protected readonly Core.Zone Zone = zone;
    protected readonly List<IActorRef> EntityActors = [];

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public abstract void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST))]
    public virtual void ReceiveZoneSupervisorBroadcast(ZONE_102_PROTOCOL.MSG_ZONESUPERVISORBROADCAST message) {
        foreach (var entity in EntityActors) {
            if (entity is null) {
                continue;
            }

            foreach (var internalMessage in message.Messages) {
                entity.Forward(internalMessage);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY))]
    public virtual void ReceiveQueryEntityObject(ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY message) {
        foreach (var entity in EntityActors) {
            if (entity is null) {
                continue;
            }

            entity.Forward(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONESTART))]
    public virtual void ReceiveZoneStart(ZONE_102_PROTOCOL.MSG_ZONESTART message) {
        foreach (var entity in EntityActors) {
            if (entity is null) {
                continue;
            }

            entity.Forward(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST))]
    public virtual void ReceiveZonePlayerBroadcast(ZONE_102_PROTOCOL.MSG_ZONEBROADCAST message) {
        foreach (var entity in EntityActors) {
            if (   message.Sender is not null 
                && entity.Path.Name == message.Sender.Path.Name 
                && message.Selfless) {
                continue;
            }

            entity.Forward(message.Message);
        }
    }

    /// <summary>
    /// Creates a new entity actor for the given core object and template.
    /// </summary>
    /// <param name="coreObject">The core object to create an actor for.</param>
    /// <param name="template">The template to use for the core object.</param>
    /// <returns>The newly created entity actor.</returns>
    protected IActorRef CreateEntityActor(CoreObject coreObject, CoreTemplate template) {
        var actorName = CreateEntityActorName(coreObject);
        var objectActor = Context.ActorOf(Props.Create(() => new ZoneEntity(coreObject, template, ZoneRef, Zone)), actorName);

        try {
            // Send a message to the object and await a reply to ensure it has been created and initialized successfully.
            var msg = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN();
            var timeout = TimeSpan.FromMilliseconds(OBJECT_CREATION_TIMEOUT_IN_MS);
            var result = objectActor.Ask<ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS>(msg, timeout).WaitAndUnwrapException();
            
            EntityActors.Add(objectActor);
        }
        catch (Exception ex) {
            var goTemplate = template as GameObjectTemplate;
            Logger.Error("Failed to create entity actor for {0} {1} ({2}).",
                Logger.Args(nameof(CoreTemplate), goTemplate.m_objectName, ex.Message));

            // Kill the actor.
            objectActor.Tell(PoisonPill.Instance);

            return null;
        }

        return objectActor;
    }

    protected static string CreateEntityActorName(CoreObject coreObject) {
        var actorName = $"{coreObject.m_debugName}_{coreObject.m_globalID}";

        // Only alphanumeric characters and underscores are allowed in actor names.
        actorName = new string([.. actorName.Where(c => char.IsLetterOrDigit(c) || c == '_')]);

        return actorName;
    }

}