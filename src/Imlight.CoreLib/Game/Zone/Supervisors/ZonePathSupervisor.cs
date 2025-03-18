/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Nito.AsyncEx.Synchronous;
using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

internal sealed class ZonePathSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // The game client splits path data, node data spawn data into three separate data entities.
        // Nodes exist in 3D space, paths connect nodes, and creatures spawn on paths.
        var pathData = message.PathData;
        var nodeData = message.NodeData;
        var creatureSpawnData = message.SpawnData;

        foreach (var path in pathData.m_pathList) {
            var nodes = GetNodesForPath(path, nodeData);
            var creatures = GetCreaturesForPath(path, creatureSpawnData);

            var pathActor = CreatePathActor(path, nodes, creatures);
            EntityActors.Add(pathActor);
        }

        // Inform the zone that the supervisor has finished loading.
        var rsp = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = GetType().Name };
        ZoneRef.Tell(rsp);
    }

    private static List<NodeObject> GetNodesForPath(PathObjectTemplate path, NodeTemplateList nodeData) {
        var nodeList = new List<NodeObject>();
        foreach (var id in path.m_nodeIDs) {
            var node = nodeData.m_nodeList.Find(n => n.m_id == id);
            if (node is not null) {
                nodeList.Add(node);
            }
            else {
                throw new Exception($"Node with ID {id.Full} not found in node data.");
            }
        }

        return nodeList;
    }

    private static List<SpawnObject> GetCreaturesForPath(PathObjectTemplate path, SpawnManager spawnData) {
        var creatureList = new List<SpawnObject>();
        foreach (var spawnObject in spawnData.m_spawners) {
            var spawnList = spawnObject.m_spawnList;
            if (spawnList is null || spawnList.Count <= 0) {
                continue;
            }

            // Check the spawn requirements for this mob, if they exist.
            // If the requirements are not met, skip this mob.
            if (spawnObject.m_globalDynamicReqs is not null) {
                var requirements = spawnObject.m_globalDynamicReqs.m_requirements.ToList();
                var operatorType = spawnObject.m_globalDynamicReqs.m_operator;
                if (!GlobalRegistryCollection.CheckGlobalRegistryRequirements(requirements, operatorType)) {
                    continue;
                }
            }

            // If the path ID matches, add it to the list.
            var matchedCreatures = spawnList.Any(x => x.m_objectInfo is not null
                                              && x.m_objectInfo.m_pathID == path.m_id);

            if (matchedCreatures) {
                creatureList.Add(spawnObject);
            }
        }

        return creatureList;
    }

    private IActorRef CreatePathActor(PathObjectTemplate path, List<NodeObject> nodes, List<SpawnObject> creatures) {
        var pathActor = Context.ActorOf(Props.Create(() => new ZonePath(path, nodes, creatures, ZoneRef, Zone)));

        try {
            // Send a message to the object and await a reply to ensure it has been created and initialized successfully.
            var msg = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN();
            var timeout = TimeSpan.FromMilliseconds(OBJECT_CREATION_TIMEOUT_IN_MS);
            var result = pathActor.Ask<ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS>(msg, timeout).WaitAndUnwrapException();
        }
        catch (Exception ex) {
            Logger.Error("Failed to create path actor {0}: {1}", Logger.Args(path.m_name, ex.Message));

            return null;
        }

        return pathActor;
    }

}