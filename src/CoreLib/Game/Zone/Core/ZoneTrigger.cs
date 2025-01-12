/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Zone.Triggers;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Represents a trigger (or event) within a <see cref="Zone"/>. Triggers are used to
/// handle events such as a gate opening, zone transfer, or other scripted events.
/// </summary>
/// <param name="zoneRef">The reference to the zone that this trigger is a part of.</param>
/// <param name="zone">The zone that this trigger is a part of.</param>
public sealed class ZoneTrigger(IActorRef zoneRef, Zone zone) : ZoneEntity(null, null, zoneRef, zone) {

    // Unsure why this override is required, but it fails without it present.
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected override void ReceiveObjectLoadBegin() => base.ReceiveObjectLoadBegin();

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // Forward the event to all triggers.
        foreach (var trigger in Components) {
            trigger.Forward(message);
        }
    }

    protected override void AutoAttachComponents() {
        // Same as the base class, except we want to search the trigger registry for
        // any triggers that should be attached to this entity.
        foreach (var (componentType, shouldAttachMethod) in ResultHandlerRegistry.GetRegisteredResultHandlers()) {
            var shouldAttach = (bool) shouldAttachMethod.Invoke(null, null);
            if (shouldAttach) {
                AddComponent(componentType);
            }
        }
    }

}