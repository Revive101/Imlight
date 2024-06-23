/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.ServerTypeCache;

namespace Imlight.CoreLib.Game.Zone;

internal class WizardZoneTriggerSupervisor : ReceiveProtocolDispatcher {
    private readonly Dictionary<Trigger, IActorRef> _triggers = new();

    // Akka.NET ctor
    public static Props Props()
        => Akka.Actor.Props.Create(() => new WizardZoneTriggerSupervisor());

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDTRIGGER))]
    private void ReceiveAddTrigger(ZONE_102_PROTOCOL.MSG_ADDTRIGGER message) {
        var triggerActor = Context.ActorOf(WizardZoneTrigger.Props());
        _triggers.Add(message.Trigger, triggerActor);

        // Tell the trigger about it's own creation.
        triggerActor.Forward(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        foreach (var (trigger, actor) in _triggers) {
            if (trigger.m_fireEvents.Any(x => x == message.EventName)) {
                actor.Forward(message);
            }
        }
    }
}
