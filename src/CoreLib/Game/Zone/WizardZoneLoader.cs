/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Formats;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Implementations;
using SharpDX;
using static Imlight.Common.Caches.ServerTypeCache;

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// A static, thread-safe class for loading zone data from the <see cref="ResourceManager" />.
/// </summary>
public static class WizardZoneLoader {
    private const string ZoneDataFileName = "gamedata.bin";
    private const string SpawnDataFileName = "spawnData.xml";
    private const string PathDataFileName = "pathData.xml";
    private const string NodeDataFileName = "pathNodeData.bin";
    private const string VolumeDataFileName = "volumes.xml";
    private const string TriggerDataFileName = "triggers.xml";
    private const string ResultCollectionName = "zone_triggers";
    private static readonly object s_lockObject = new();
    private static readonly List<string> s_blacklistedObjectActives = new()
    {
        "EditorOnly",
        "PetOnly",
    };

    private static WizardZone s_zone;
    private static IActorRef s_zoneActorRef;
    private static KiWad s_wad;
    private static TypeCache.WizZoneData s_zoneData;
    private static TypeCache.SpawnManager s_spawnData;
    private static TypeCache.PathManager_PathTemplateList s_pathData;
    private static TypeCache.PathManager_NodeTemplateList s_nodeData;
    private static WizZoneVolumes s_zoneVolumes;
    private static WizZoneTriggers s_zoneTriggers;

    /// <summary>
    /// Loads the <see cref="WizardZone" /> data from the <see cref="ResourceManager" />.
    /// </summary>
    /// <param name="zone">The zone object.</param>
    /// <param name="zoneActorRef">The Akka.NET actor reference of the zone.</param>
    public static void LoadZoneData(WizardZone zone, IActorRef zoneActorRef) {
        lock (s_lockObject) {
            s_zone = zone;
            s_zoneActorRef = zoneActorRef;

            if (!ResourceManager.TryLoadFile(zone.ZoneName, out s_wad)) {
                Logger.Error("Zone {ZoneName} tried to load its own data, but none was " +
                             "found in the {Name}. We will continue, but the zone will not " +
                             "contain any objects, mobs or volumes.",
                    Logger.Args(zone.ZoneName, nameof(ResourceManager)));
                return;
            }

            LoadZoneData();
            LoadSpawnData();
            LoadPathData();
            LoadNodeData();
            LoadVolumeData();
            LoadTriggerData();
            CreateZoneCoreObjects();
            CreateZoneCombatSigils();
            CreateZonePaths();
            CreateZoneVolumes();
            CreateZoneTriggers();

            ClearUnmanagedMemory();
        }
    }

    /// <summary>
    /// Loads the zone data from the KIWAD file.
    /// </summary>
    private static void LoadZoneData() {
        Logger.Verbose("Loading zone data for {ZoneName}...", Logger.Args(s_zone.ZoneName));

        var serializer = new FileSerializer();
        s_zoneData = serializer.OpenClass<TypeCache.WizZoneData>(s_wad, ZoneDataFileName);

        if (s_zoneData is null) {
            Logger.Error("Zone {ZoneName} could not load {ZoneDataFileName} as it was missing or invalid.",
                Logger.Args(s_zone.ZoneName, ZoneDataFileName));
        }
    }

    /// <summary>
    /// Loads the spawn data from the KIWAD file.
    /// </summary>
    private static void LoadSpawnData() {
        Logger.Verbose("Loading spawn data for {ZoneName}...", Logger.Args(s_zone.ZoneName));

        var serializer = new FileSerializer();
        s_spawnData = serializer.OpenClass<TypeCache.SpawnManager>(s_wad, SpawnDataFileName);

        if (s_spawnData is null) {
            Logger.Error("Zone {Name} could not load {SpawnDataFileName} as it was missing or invalid.",
                Logger.Args(s_zone.ZoneName, SpawnDataFileName));
        }
    }

    /// <summary>
    /// Loads the path data from the KIWAD file.
    /// </summary>
    private static void LoadPathData() {
        Logger.Verbose("Loading path data for {ZoneName}...", Logger.Args(s_zone.ZoneName));

        var serializer = new FileSerializer();
        s_pathData = serializer.OpenClass<TypeCache.PathManager_PathTemplateList>(s_wad, PathDataFileName);

        if (s_pathData is null) {
            Logger.Error(
                "Zone {Name} could not load {PathDataFileName} as it was missing or invalid.",
                Logger.Args(s_zone.ZoneName, PathDataFileName));
        }
    }

    /// <summary>
    /// Loads the node data from the KIWAD file.
    /// </summary>
    private static void LoadNodeData() {
        Logger.Verbose("Loading node data for {ZoneName}...", Logger.Args(s_zone.ZoneName));

        var serializer = new FileSerializer();
        s_nodeData = serializer.OpenClass<TypeCache.PathManager_NodeTemplateList>(s_wad, NodeDataFileName);

        if (s_nodeData is null) {
            Logger.Error(
                "Zone {Name} could not load {NodeDataFileName} as it was missing or invalid.",
                Logger.Args(s_zone.ZoneName, NodeDataFileName));
        }
    }

    /// <summary>
    /// Load the volume data from the KIWAD file.
    /// </summary>
    private static void LoadVolumeData() {
        Logger.Verbose("Loading volume data for {ZoneName}...", Logger.Args(s_zone.ZoneName));

        var serializer = new FileSerializer();
        s_zoneVolumes = serializer.OpenClass<WizZoneVolumes>(s_wad, VolumeDataFileName);

        if (s_zoneVolumes is null) {
            Logger.Error(
                "Zone {Name} could not load {VolumeDataFileName} as it was missing or invalid.",
                Logger.Args(s_zone.ZoneName, VolumeDataFileName));
        }
    }

    /// <summary>
    /// Load the trigger data from the KIWAD file.
    /// </summary>
    private static void LoadTriggerData() {
        Logger.Verbose("Loading trigger data for {ZoneName}...", Logger.Args(s_zone.ZoneName));

        var serializer = new FileSerializer();
        s_zoneTriggers = serializer.OpenClass<WizZoneTriggers>(s_wad, TriggerDataFileName);

        if (s_zoneTriggers is null) {
            Logger.Error(
                "Zone {Name} could not load {TriggerDataFileName} as it was missing or invalid.",
                Logger.Args(s_zone.ZoneName, TriggerDataFileName));
        }
    }

    /// <summary>
    /// Creates game objects for the zone based on the loaded zone data.
    /// </summary>
    private static void CreateZoneCoreObjects() {
        if (s_zoneData is null) {
            return;
        }

        foreach (var objectInfo in s_zoneData.m_objectList
            .Where(info => info != null)
            .Where(info => info is not CombatSigil)) {
            if (!ShouldLoadCoreObject(objectInfo)) {
                continue;
            }

            var template = (TypeCache.GameObjectTemplate) CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            var newObject = CoreObjectFactory.CreateObjectFromTemplate(objectInfo, template, objectInfo.m_templateID);
            if (newObject == null) {
                continue;
            }

            var message = new ZONE_102_PROTOCOL.MSG_ADDOBJECT {
                CoreObject = newObject,
                Template = template
            };
            s_zoneActorRef.Tell(message);
        }
    }

    /// <summary>
    /// Creates combat sigils in the zone based on the loaded zone data.
    /// </summary>
    private static void CreateZoneCombatSigils() {
        foreach (var objectInfo in s_zoneData.m_objectList
                .Where(info => info != null)
                .Where(info => info is CombatSigil)) {
            var template = (TypeCache.GameObjectTemplate) CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            var newObject = CoreObjectFactory.CreateObjectFromTemplate(objectInfo, template, objectInfo.m_templateID);
            if (newObject == null) {
                continue;
            }

            var message = new ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL {
                CoreObject = newObject,
                Template = template
            };
            s_zoneActorRef.Tell(message);
        }
    }

    /// <summary>
    /// Checks if a given object should be loaded into the zone based on zone and world events.
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    private static bool ShouldLoadCoreObject(TypeCache.CoreObjectInfo info) {
        if (info.m_spawnRequirements is null) {
            return true;
        }

        var allMatched = info.m_spawnRequirements.m_operator == TypeCache.Requirement.Operator.ROP_OR;

        foreach (var requirement in info.m_spawnRequirements.m_requirements) {
            if (requirement is TypeCache.ReqGlobalRegistryValue globalReq) {
                var globalValue = GlobalRegistry.GetRegistryEntry(globalReq.m_entryName);

                switch (globalReq.m_operatorType) {
                    case TypeCache.ReqNumeric.OPERATOR_TYPE.OPERATOR_EQUALS:
                        allMatched = allMatched && (globalReq.m_numericValue == globalValue);
                        break;
                    case TypeCache.ReqNumeric.OPERATOR_TYPE.OPERATOR_LESS_THAN:
                        allMatched = allMatched && (globalReq.m_numericValue < globalValue);
                        break;
                    case TypeCache.ReqNumeric.OPERATOR_TYPE.OPERATOR_LESS_THAN_EQ:
                        allMatched = allMatched && (globalReq.m_numericValue <= globalValue);
                        break;
                    case TypeCache.ReqNumeric.OPERATOR_TYPE.OPERATOR_GREATER_THAN:
                        allMatched = allMatched && (globalReq.m_numericValue > globalValue);
                        break;
                    case TypeCache.ReqNumeric.OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ:
                        allMatched = allMatched && (globalReq.m_numericValue >= globalValue);
                        break;
                    case TypeCache.ReqNumeric.OPERATOR_TYPE.OPERATOR_UNKNOWN:
                    default: {
                            Logger.Error("Zone {ZoneName} contains a spawn requirement that " +
                                              "references a global registry value that does not exist. " +
                                              "Entry name: {EntryName}", Logger.Args(s_zone.ZoneName, globalReq.m_entryName));
                            break;
                        }
                }

                allMatched = allMatched && !globalReq.m_applyNOT;
            }
            else {
                Logger.Warning("Holy!!! We found a spawn requirement that isn't a global registry value. " +
                            "This is a problem. Let Jooty know.");
            }
        }

        return allMatched;
    }

    /// <summary>
    /// Creates paths for the zone based on the loaded path data.
    /// </summary>
    private static void CreateZonePaths() {
        foreach (var path in s_pathData.m_pathList) {
            var nodeList = GetNodesForPath(path);
            var creatureList = GetCreaturesForPath(path);

            var msg = new ZONE_102_PROTOCOL.MSG_ADDPATH {
                Id = path.m_id,
                Name = path.m_name,
                Nodes = nodeList,
                Creatures = creatureList
            };
            s_zoneActorRef.Tell(msg);
        }
    }

    /// <summary>
    /// Creates the volumes for the zone based on the loaded volume data.
    /// </summary>
    /// <exception cref="NullReferenceException"></exception>
    private static void CreateZoneVolumes() {
        if (s_zoneVolumes is null) {
            throw new NullReferenceException(nameof(s_zoneVolumes));
        }

        if (s_zoneTriggers is null) {
            throw new NullReferenceException(nameof(s_zoneTriggers));
        }

        foreach (var volume in s_zoneVolumes.m_volumes) {
            var newObj = CoreObjectFactory.CreateObjectFromInfo(volume, volume.m_templateID);
            if (newObj is null) {
                continue;
            }

            // Set data for this CoreObject from the given volume data.
            var loc = new Vector3(volume.m_locationX, volume.m_locationY, volume.m_locationZ);
            newObj.m_location = loc;
            // For some reason, the volume type has two `m_templateID` fields, but only the duplicate one is used.
            newObj.m_templateID = volume.m_templateID; // I've never seen this templateID be anything but 1700.

            // Write a message citing the details of this volume, and send a message to the zone.
            var msg = new ZONE_102_PROTOCOL.MSG_ADDVOLUME {
                CoreObject = newObj,
                Volume = volume,
            };
            s_zoneActorRef.Tell(msg);
        }
    }

    /// <summary>
    /// Creates the triggers for the zone based on the loaded trigger data.
    /// </summary>
    private static void CreateZoneTriggers() {
        if (s_zoneTriggers is null) {
            throw new NullReferenceException(nameof(s_zoneTriggers));
        }

        var zoneName = s_zoneData.m_zoneName;
        var persistentZoneData = ZoneDataCollection.GetZoneData(zoneName);

        foreach (var trigger in s_zoneTriggers.m_triggers) {
            // If there's persistent data associated with this trigger, load it.
            var persistentTriggerData = persistentZoneData?.Teleports
                .FirstOrDefault(x => x.TriggerName == trigger.m_triggerName);
            if (persistentTriggerData is not null) {
                // Set the trigger results to the results stored in the database.
                var resultList = new TypeCache.ResultList { m_results = new List<TypeCache.Result> { persistentTriggerData.Teleport } };
                trigger.m_results = resultList;
            }

            // Write a message citing the details of this volume, and send a message to the zone.
            var msg = new ZONE_102_PROTOCOL.MSG_ADDTRIGGER { Trigger = trigger };
            s_zoneActorRef.Tell(msg);
        }
    }

    /// <summary>
    /// Retrieves a list of node objects for a given path.
    /// </summary>
    /// <param name="path">The path object template.</param>
    /// <returns>A list of node objects.</returns>
    private static List<TypeCache.NodeObject> GetNodesForPath(TypeCache.PathObjectTemplate path) {
        var nodeList = new List<TypeCache.NodeObject>();
        foreach (var id in path.m_nodeIDs) {
            var node = s_nodeData.m_nodeList.Find(n => n.m_id == id);
            if (node is not null) {
                nodeList.Add(node);
            }
            else {
                Logger.Warning("Zone [{Name}] contained a path {PathName} with a node that could not be found. " +
                            "Node ID: {Id}",
                    Logger.Args(s_zone.ZoneName, path.m_name, id));
            }
        }

        return nodeList;
    }

    /// <summary>
    /// Retrieves a list of creature objects for a given path.
    /// </summary>
    /// <param name="path">The path object template.</param>
    /// <returns>A list of creature objects.</returns>
    private static List<TypeCache.SpawnObject> GetCreaturesForPath(TypeCache.PathObjectTemplate path) {
        var creatureList = new List<TypeCache.SpawnObject>();
        foreach (var spawnObject in s_spawnData.m_spawners) {
            var spawnList = spawnObject.m_spawnList;
            if (spawnList is null || spawnList.Count <= 0 || spawnList[0]?.m_objectInfo is null) {
                continue;
            }

            // If the ID matches, add it to the creature list of this path.
            if (spawnList[0].m_objectInfo.m_pathID == path.m_id) {
                creatureList.Add(spawnObject);
            }

            // TEST: I'm not sure this will ever occur. Perhaps remove later?
            if (!AllObjectsContainSamePath(spawnList)) {
                Logger.Warning("Zone {ZoneName} contains a SpawnObject {SoName} " +
                                   "that contains multiple objects, which spawn on different paths. Let Jooty know.",
                    Logger.Args(s_zone.ZoneName, spawnObject.m_name));
            }
        }

        return creatureList;
    }

    /// <summary>
    /// Checks to see if all creatures in a <see cref="TypeCache.SpawnItem"/> contain the same path ID.
    /// </summary>
    /// <param name="spawnList">The list of spawns.</param>
    /// <returns>True, if all the creatures are on the same path; false otherwise.</returns>
    private static bool AllObjectsContainSamePath(IReadOnlyList<TypeCache.SpawnItem> spawnList) {
        var firstPathId = spawnList[0].m_objectInfo.m_pathID;
        return spawnList.All(x => x.m_objectInfo.m_pathID == firstPathId);
    }

    /// <summary>
    /// Clears any memory used so the next lock iteration may have a clean slate.
    /// </summary>
    private static void ClearUnmanagedMemory() {
        s_wad = null;
        s_zone = null;
        s_zoneActorRef = null;
        s_wad = null;
        s_zoneData = null;
        s_spawnData = null;
        s_pathData = null;
        s_nodeData = null;
        s_zoneVolumes = null;
        s_zoneTriggers = null;
    }

    /// <summary>
    /// Sanitizes a column name for use in the database.
    /// </summary>
    /// <param name="colName"></param>
    /// <returns></returns>
    private static string SanitizeColName(string colName) {
        // Use regular expression to remove any character that isn't an alphabet character or an underscore.
        return Regex.Replace(colName, @"[^a-zA-Z_]", "");
    }
}
