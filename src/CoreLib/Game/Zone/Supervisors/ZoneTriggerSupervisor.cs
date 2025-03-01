/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Configuration;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Nito.AsyncEx.Synchronous;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.ServerTypeCache;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor
/// for any triggers that may happen with the zone.
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
/// <remarks>Triggers are any event that can happen within a zone. Zone transfers, POI
/// text, etc.</remarks>
internal sealed class ZoneTriggerSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    private static readonly bool _randomizeGateways = ConfigurationManager.Settings.RandomizeGateways;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // Triggers are stored in the client data, except for zone transfers.
        // Our QA team has manually recreated this trigger data, and is available within the database.
        // Words cannot describe how thankful I am for QA. They are the unsung heroes of the development team.
        var replacedTriggers = ReplaceTriggerDataWithDatabase(message.TriggerData);

        foreach (var trigger in replacedTriggers) {
            _ = CreateTriggerActor(trigger);
        }

        // Inform the zone that we have finished initializing all objects.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = nameof(ZoneTriggerSupervisor) };
        Sender.Tell(reply);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // Forward the event to all triggers.
        foreach (var trigger in EntityActors) {
            trigger.Forward(message);
        }
    }

    private List<Trigger> ReplaceTriggerDataWithDatabase(WizZoneTriggers clientTriggers) {
        var triggers = new List<Trigger>(clientTriggers.m_triggers);
        var zoneName = Zone.ZonePath;

        // One single database query: great!
        var databaseTriggers = ZoneDataCollection.GetZoneData(zoneName);

        foreach (var trigger in triggers) {
            if (trigger is null) {
                continue;
            }

            // If there's persistent data associated with this trigger, load it.
            var persistentTriggerData = databaseTriggers?.Teleports
                .FirstOrDefault(x => x.TriggerName == trigger.m_triggerName);

            if (persistentTriggerData is not null) {
                // April Fools: if we've confirmed this is a zone transfer, we'll instead
                // grab a random zone transfer from the database. Then, we'll set the trigger
                // results to the random zone transfer.
                if (_randomizeGateways) {
                    var randomZoneData = ZoneDataCollection.GetAprilFoolsRandomZoneData();
                    var randomIdx = new Random().Next(randomZoneData.Teleports.Count);
                    var randomZoneTransfer = randomZoneData.Teleports[randomIdx];

                    persistentTriggerData.Teleport = randomZoneTransfer.Teleport;
                }

                // Set the trigger results to the results stored in the database.
                var resultList = new ResultList {
                    m_results = [persistentTriggerData.Teleport]
                };
                trigger.m_results = resultList;
            }
        }

        return triggers;
    }

    private IActorRef CreateTriggerActor(Trigger trigger) {
        var objectActor = Context.ActorOf(Props.Create(() => new ZoneTrigger(ZoneRef, Zone, trigger)));

        try {
            // Send a message to the object and await a reply to ensure it has been created and initialized successfully.
            var msg = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN();
            var timeout = TimeSpan.FromMilliseconds(OBJECT_CREATION_TIMEOUT_IN_MS);
            var result = objectActor.Ask<ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS>(msg, timeout).WaitAndUnwrapException();

            EntityActors.Add(objectActor);
        }
        catch (Exception ex) {
            Logger.Error("Failed to create trigger actor for {0} {1} ({2}).",
                Logger.Args(nameof(Trigger), trigger.m_triggerName, ex.Message));

            return null;
        }

        return objectActor;
    }

}