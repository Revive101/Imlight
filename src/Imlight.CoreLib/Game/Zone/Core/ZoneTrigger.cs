/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
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
 * Last Updated: 08/21/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
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
    private readonly Dictionary<ulong, DateTime> _cooldowns = [];

    // Unsure why this override is required, but it fails without it present.
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected override void ReceiveObjectLoadBegin(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN message)
        => base.ReceiveObjectLoadBegin(message);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // Early-out if the event name doesn't match any configured fire events.
        if (!TriggerData.m_fireEvents.Any(x => x == message.EventName)) {
            return;
        }

        var needsRequirements = !message.RequirementsChecked
            && TriggerData.m_requirements?.m_requirements is { Count: > 0 };

        // Ask for the wizard without blocking; the event comes back with it attached.
        if (needsRequirements && !PostEventWizardQuery.TryResolve(Self, Sender, message)) {
            return;
        }

        // Early-out for per-player cooldowns.
        if (TriggerData.m_cooldown > 0 && !CooldownCheck(message.PlayerGameObject)) {
            return;
        }

        // Evaluate requirements when present.
        if (needsRequirements) {
            var requirementsMet = RequirementDispatcher.EvaluateRequirements(
                requirements: TriggerData.m_requirements,
                context: new ZoneRequirementContext(
                    TriggerData.m_requirements,
                    message.PlayerActor,
                    message.PlayerGameObject,
                    message.PlayerWizard,
                    ZoneRef,
                    TriggerData.m_triggerName
                ) {
                    ScriptState = Zone.ScriptState,
                    EventAdjectives = message.Adjectives,
                }
            );

            if (!requirementsMet) {
                return;
            }
        }

        // Paired teleporter triggers share an event; the supervisor keeps only the first
        // ResTeleport, so the player is never raced between destinations.
        var results = TriggerData.m_results;
        if (message.SuppressTeleportResults && results?.m_results is { Count: > 0 }) {
            results = new ResultList {
                m_results = results.m_results.Where(result => result is not ResTeleport).ToList()
            };
        }

        // A result that posts one of this trigger's own fire events would fire it again without end,
        // and the event that fired it has already reached every trigger and quest goal.
        if (results?.m_results is { Count: > 0 } && results.m_results.Any(IsSelfPost)) {
            results = new ResultList {
                m_results = results.m_results.Where(result => !IsSelfPost(result)).ToList()
            };
        }

        ResultDispatcher.ExecuteResults(Context, results, message.PlayerActor, message.PlayerGameObject,
                                       zoneActor: ZoneRef, triggerName: TriggerData.m_triggerName,
                                       scriptState: Zone.ScriptState, playerSpawned: message.PlayerSpawned);
    }

    /// <summary>
    /// True when a requirement class the client data uses failed to decode (it came back null). Such a trigger
    /// must not fire: skipping the requirement would make it pass for every event.
    /// </summary>
    internal static bool HasUndecodableRequirement(RequirementList requirements)
        => requirements?.m_requirements is { } list
        && list.Any(r => r is null || r is RequirementList nested && HasUndecodableRequirement(nested));

    private bool IsSelfPost(Result result)
        => result switch {
            ResPostEvent post => TriggerData.m_fireEvents.Any(x => x == post.m_eventName),
            ResModifyTriggerObject modify => TriggerData.m_fireEvents.Any(x => x == $"{modify.m_triggerObjName}.{modify.m_triggerObjState}.EnterState"),
            _ => false
        };

    private bool CooldownCheck(CoreObject playerObject) {
        var key = playerObject?.m_globalID ?? 0;
        if (_cooldowns.TryGetValue(key, out var lastTriggered)
            && DateTime.Now - lastTriggered < TimeSpan.FromSeconds(TriggerData.m_cooldown)) {
            return false;
        }

        _cooldowns[key] = DateTime.Now;

        return true;
    }

}
