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
using System.Reflection;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
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
public sealed class ZoneTrigger(IActorRef zoneRef, Zone zone, Trigger trigger) 
    : ZoneEntity(null, null, zoneRef, zone) {

    private const uint RESULT_HANDLER_TIMEOUT_IN_MS = 5000;

    public Trigger TriggerData { get; init; } = trigger;
    private readonly Dictionary<IActorRef, DateTime> _cooldowns = [];
    private readonly List<IActorRef> _triggerActors = [];

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

            // Fire off all results that happen on this event.
            // We do that by simply dispatching the event to all components attached to this trigger.
            foreach (var triggerActor in _triggerActors) {
                triggerActor.Ask<ZONE_102_PROTOCOL.MSG_RESULTEXECUTED>(
                    message,
                    timeout: TimeSpan.FromMilliseconds(RESULT_HANDLER_TIMEOUT_IN_MS)
                );
            }
        }
    }

    protected override void AutoAttachComponents() {
        foreach (var (handlerType, shouldAttachMethod) in ResultHandlerRegistry.GetRegisteredResultHandlers()) {
            // If it's a generic type definition, we need to construct it with the correct type
            if (handlerType.IsGenericTypeDefinition) {
                // Get the result types from the trigger data
                var results = TriggerData?.m_results?.m_results;
                if (results == null) {
                    continue;
                }

                // Get the generic parameter constraints
                var genericParam = handlerType.GetGenericArguments()[0];
                var constraints = genericParam.GetGenericParameterConstraints();

                // Find the first result that matches our constraints
                var matchingResult = results
                    .Where(r => r != null)
                    .FirstOrDefault(r => constraints.All(c => c.IsAssignableFrom(r.GetType())));

                if (matchingResult != null) {
                    // Create the constructed type with our result type
                    var constructedType = handlerType.MakeGenericType(matchingResult.GetType());

                    // Get the method from the constructed type
                    var constructedMethod = constructedType.GetMethod(
                        "ShouldAttachToEntity",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy
                    );

                    var shouldAttach = (bool) constructedMethod.Invoke(null, [this]);
                    if (shouldAttach) {
                        AddComponent(constructedType);
                    }
                }
            }
            else {
                // Handle non-generic types as before
                var shouldAttach = (bool) shouldAttachMethod.Invoke(null, [this]);
                if (shouldAttach) {
                    AddComponent(handlerType);
                }
            }
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

    private new void AddComponent(System.Type type) {
        var props = Props.Create(type, this);
        var componentName = type.Name;

        // Ensure the component name is valid for an actor path.
        if (string.IsNullOrEmpty(componentName) || componentName.StartsWith('$') || !IsValidActorName(componentName)) {
            componentName = $"Component_{Guid.NewGuid()}";
        }

        // Create the component actor and request its identity.
        var componentActor = Context.ActorOf(props, componentName);

        _triggerActors.Add(componentActor);
    }

    private static bool IsValidActorName(string name) {
        foreach (char c in name) {
            if (!char.IsLetterOrDigit(c) && !"-_.*$+:@&=,!~';()".Contains(c)) {
                return false;
            }
        }
        
        return true;
    }

}