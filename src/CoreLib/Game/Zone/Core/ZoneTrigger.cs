/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.IO;
using Imlight.CoreLib.Game.Zone.Triggers;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using System;
using System.Collections.Generic;
using static Imlight.Common.Caches.ServerTypeCache;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Represents a trigger (or event) within a <see cref="Zone"/>. Triggers are used to
/// handle events such as a gate opening, zone transfer, or other scripted events.
/// </summary>
/// <param name="zoneRef">The reference to the zone that this trigger is a part of.</param>
/// <param name="zone">The zone that this trigger is a part of.</param>
public sealed class ZoneTrigger : ZoneEntity {

    private readonly Trigger _trigger;
    private readonly List<ByteString> _combinedEvents;
    private readonly Dictionary<IActorRef, DateTime> _cooldowns = [];

    // ctor
    public ZoneTrigger(IActorRef zoneRef, Zone zone, Trigger trigger) : base(null, null, zoneRef, zone) {
        _trigger = trigger;
        _combinedEvents = [.. _trigger.m_activateEvents, .. _trigger.m_deactivateEvents];
    }

    // Unsure why this override is required, but it fails without it present.
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected override void ReceiveObjectLoadBegin() => base.ReceiveObjectLoadBegin();

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // Determine if this event name matches either or enter or exit events.
        if (_combinedEvents.Contains(message.EventName)) {
            // If the event name matches, we'll also want to check if the player is on cooldown.
            if (_trigger.m_cooldown > 0 && !CooldownCheck(message.PlayerActor)) {
                return;
            }

            // Fire off all results that happen on this event.
            // We do that by simply dispatching the event to all components attached to this trigger.
            foreach (var component in Components) {
                component.Tell(message);
            }
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

    private bool CooldownCheck(IActorRef playerRef) {
        if (_cooldowns.TryGetValue(playerRef, out var lastTriggered)) {
            if (DateTime.Now - lastTriggered < TimeSpan.FromSeconds(_trigger.m_cooldown)) {
                return false;
            }
            else {
                _cooldowns[playerRef] = DateTime.Now;
            }
        }
        else {
            _cooldowns.Add(playerRef, DateTime.Now);
        }

        return true;
    }

}