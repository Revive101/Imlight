/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Game.Events;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.ServerTypeCache;

namespace Imlight.CoreLib.Game.Zone;

internal class WizardZoneTrigger : ReceiveProtocolDispatcher {
    private readonly Dictionary<IActorRef, DateTime> _cooldowns = new();
    private Trigger _trigger;

    // Akka.NET ctor
    public static Props Props()
        => Akka.Actor.Props.Create(() => new WizardZoneTrigger());

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDTRIGGER))]
    private void ReceiveAddTrigger(ZONE_102_PROTOCOL.MSG_ADDTRIGGER message) {
        this._trigger = message.Trigger;
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // todo: requirements
        // todo: cooldowns

        foreach (var ev in _trigger.m_results.m_results) {
            ResultDispatcher.DispatchResult(this.Sender, message.SenderActor, ev);
        }
    }
}
