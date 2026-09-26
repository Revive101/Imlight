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
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Nito.AsyncEx.Synchronous;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor
/// for any triggers that may happen with the zone.
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
/// <remarks>Triggers are any event that can happen within a zone. Zone transfers, POI
/// text, etc.</remarks>
internal sealed class ZoneTriggerSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    private static readonly bool s_randomizeGateways 
        = ConfigurationManager.Settings["April Fools.RandomizeGateways"].AsBool();

    private readonly List<(Trigger Trigger, IActorRef Actor)> _orderedTriggers = [];

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // Triggers are stored in the client data, except for zone transfers.
        // Our QA team has manually recreated this trigger data, and is available within the database.
        // Words cannot describe how thankful I am for QA. They are the unsung heroes of the development team.
        var replacedTriggers = ReplaceTriggerDataWithDatabase(message.TriggerData);
        var spawners = message.SpawnData.m_spawners;
        UpdateSpawnResultTriggers(ref replacedTriggers, spawners, message.PathData.m_pathList, message.NodeData.m_nodeList);

        _orderedTriggers.Clear();
        foreach (var trigger in replacedTriggers) {
            var triggerActor = CreateTriggerActor(trigger);
            if (triggerActor is not null) {
                _orderedTriggers.Add((trigger, triggerActor));
            }
        }

        // Inform the zone that we have finished initializing all objects.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = nameof(ZoneTriggerSupervisor) };
        Sender.Tell(reply);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // Client wads pair multiple triggers on the same event; every passing trigger fires
        // (their results are independent), but paired teleporter triggers must not both win:
        // only the first ResTeleport in wad order may execute.
        var teleportDispatched = false;
        foreach (var (trigger, triggerActor) in _orderedTriggers) {
            if (trigger.m_fireEvents is null || !trigger.m_fireEvents.Any(x => x == message.EventName)) {
                continue;
            }

            if (!EvaluateRequirements(trigger, message)) {
                continue;
            }

            var hasTeleportResult = trigger.m_results?.m_results?.Any(result => result is ResTeleport) == true;
            var suppressTeleportResults = hasTeleportResult && teleportDispatched;
            teleportDispatched |= hasTeleportResult;

            triggerActor.Forward(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = message.EventName,
                PlayerActor = message.PlayerActor,
                PlayerGameObject = message.PlayerGameObject,
                SuppressTeleportResults = suppressTeleportResults,
            });
        }
    }

    private bool EvaluateRequirements(Trigger trigger, ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        if (trigger.m_requirements?.m_requirements is null || trigger.m_requirements.m_requirements.Count == 0) {
            return true;
        }

        var queryWizardMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var wizardResponse = message.PlayerActor.Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryWizardMsg).Result;

        return RequirementDispatcher.EvaluateRequirements(
            requirements: trigger.m_requirements,
            context: new ZoneRequirementContext(
                trigger.m_requirements,
                message.PlayerActor,
                message.PlayerGameObject,
                wizardResponse.Wizard,
                ZoneRef,
                trigger.m_triggerName));
    }

    private void UpdateSpawnResultTriggers(ref List<Trigger> clientTriggers, List<SpawnObject> spawners, List<PathObjectTemplate> paths, List<NodeObject> nodes) {
        foreach (var trigger in clientTriggers) {
            if (trigger.m_results == null || trigger.m_results.m_results == null) {
                continue;
            }

            foreach (var result in trigger.m_results.m_results) {
                if (result is ResSpawn resSpawn) {
                    var spawnObjectList = spawners.Find(x => x.m_id == resSpawn.m_spawnID);
                    if (spawnObjectList != null) {
                        var spawnObject = spawnObjectList.m_spawnList[0];
                        var objectPath = paths.Find(x => x.m_id.Full == spawnObject.m_objectInfo.m_pathID.Full);
                        resSpawn.nodes = nodes.FindAll(x => objectPath.m_nodeIDs.Contains(x.m_id));
                        resSpawn.templateID = spawnObject.m_objectInfo.m_templateID;
                    }
                }
                else if (result is ResDespawn resDespawn) {
                    // The wad data only names the spawner; resolve the template so the removal can match an entity.
                    var spawnObjectList = spawners.Find(x => x.m_id == resDespawn.m_spawnID);
                    if (spawnObjectList != null) {
                        resDespawn.m_templateID = spawnObjectList.m_spawnList[0].m_objectInfo.m_templateID;
                    }
                }
            }
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
                if (s_randomizeGateways) {
                    var randomZoneData = ZoneDataCollection.GetAprilFoolsRandomZoneData();
                    var randomIdx = new Random().Next(randomZoneData.Teleports.Count - 1);
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