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
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imcodec.ObjectProperty.TypeCache;
using Microsoft.VisualBasic;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

internal sealed class ZonePathSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // The game client splits path data, node data spawn data into three separate data entities.
        // Nodes exist in 3D space, paths connect nodes, and creatures spawn on paths.
        var pathData = message.PathData;
        var nodeData = message.NodeData;
        var creatureSpawnData = message.SpawnData;

        var nodesById = new Dictionary<ulong, NodeObject>();
        foreach (var node in nodeData.m_nodeList) {
            nodesById.TryAdd(node.m_id.Full, node);
        }

        foreach (var path in pathData.m_pathList) {
            if (!TryGetNodesForPath(path, nodesById, out var nodes)) {
                continue;
            }

            var creatures = GetCreaturesForPath(path, creatureSpawnData);
            var pathActor = Context.ActorOf(Props.Create(() => new ZonePath(path, nodes, creatures, ZoneRef, Zone)));
            BeginEntityLoad(pathActor, path.m_name);
        }

        ReportLoadedWhenEntitiesLoad();
    }

    private static bool TryGetNodesForPath(PathObjectTemplate path, Dictionary<ulong, NodeObject> nodesById,
                                           out List<NodeObject> nodes) {
        nodes = new List<NodeObject>(path.m_nodeIDs.Count);
        foreach (var id in path.m_nodeIDs) {
            if (!nodesById.TryGetValue(id.Full, out var node)) {
                Logger.Error("Path {Path} names node {NodeId}, which is not in the node data; skipping the path.",
                    Logger.Args(path.m_name, id.Full));

                return false;
            }

            nodes.Add(node);
        }

        return true;
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
                var requirementsMet = RequirementDispatcher.EvaluateRequirements(
                    requirements: spawnObject.m_globalDynamicReqs,
                    context: new ZoneRequirementContext(
                        requirements: spawnObject.m_globalDynamicReqs,
                        playerRef: null,
                        playerObj: null,
                        wizard: null,
                        zoneRef: null,
                        triggerName: null
                    )
                );

                if (!requirementsMet) {
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

}
