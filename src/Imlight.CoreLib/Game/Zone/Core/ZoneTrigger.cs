/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * TRIGGER SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Handles event-based scripting within zones, allowing for interactive
 * elements like gates, teleporters, and scripted sequences.
 * 
 * USAGE EXAMPLE:
 * // Created by the Zone system during zone loading
 * // Trigger triggerData = ...
 * var triggerActor = Context.ActorOf(Props.Create(() => 
 *     new ZoneTrigger(zoneRef, zone, triggerData)));
 * 
 * NOTE:
 * Triggers are usually activated by volumes
 * Supports player-specific cooldowns for trigger activation.
 * Dynamically attaches result handlers based on trigger configuration.
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Results;
using Imlight.CoreLib.Game.Results.Contexts;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Represents a trigger (or event) within a <see cref="Zone"/>. Triggers are used to
/// handle events such as a gate opening, zone transfer, or other scripted events.
/// </summary>
/// <param name="zoneRef">The reference to the zone that this trigger is a part of.</param>
/// <param name="zone">The zone that this trigger is a part of.</param>
public sealed class ZoneTrigger(IActorRef zoneRef, Zone zone, Trigger trigger) 
    : ZoneEntity(null, null, null, zoneRef, zone) {

    public Trigger TriggerData { get; init; } = trigger;
    private readonly Dictionary<IActorRef, DateTime> _cooldowns = [];

    // Unsure why this override is required, but it fails without it present.
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected override void ReceiveObjectLoadBegin() 
        => base.ReceiveObjectLoadBegin();

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // Determine if this event name matches either or enter or exit events.
        if (TriggerData.m_fireEvents.Any(x => x == message.EventName)) {
            // If the event name matches, we'll also want to check if the player is on cooldown.
            if (TriggerData.m_cooldown > 0 && !CooldownCheck(message.PlayerActor)) {
                return;
            }

            ResultDispatcher.ExecuteResults(Context, TriggerData.m_results, message.PlayerActor, message.PlayerGameObject, 
                                           Sender, ZoneRef, triggerName: TriggerData.m_triggerName);
        }
    }


    private bool CooldownCheck(IActorRef playerRef) {
        if (_cooldowns.TryGetValue(playerRef, out var lastTriggered)) {
            if (DateTime.Now - lastTriggered < TimeSpan.FromSeconds(TriggerData.m_cooldown)) {
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