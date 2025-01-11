/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.WizardZone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.WizardZone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor
/// for any entities that are created within the zone.
/// </summary>
/// <param name="wizardZoneRef">The reference to the parent <see cref="WizardZone"/>.</param>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
internal abstract class ZoneEntitySupervisor(IActorRef wizardZoneRef, Core.Zone zone) : ReceiveProtocolDispatcher {

    protected readonly IActorRef ZoneRef = wizardZoneRef;
    protected readonly Core.Zone Zone = zone;
    protected readonly List<IActorRef> EntityActors = [];

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST message) {
        foreach (var actor in EntityActors) {
            foreach (var internlMessage in message.Messages) {
                actor.Tell(internlMessage);
            }
        }
    }

    /// <summary>
    /// Creates a new entity actor for the given core object and template.
    /// </summary>
    /// <param name="coreObject">The core object to create an actor for.</param>
    /// <param name="template">The template to use for the core object.</param>
    /// <returns>The newly created entity actor.</returns>
    protected IActorRef CreateEntityActor(CoreObject coreObject, CoreTemplate template) {
        var objectActor = Context.ActorOf(Props.Create(() => new ZoneEntity(coreObject, template, ZoneRef, Zone)));
        EntityActors.Add(objectActor);

        // todo: await reply

        return objectActor;
    }

}