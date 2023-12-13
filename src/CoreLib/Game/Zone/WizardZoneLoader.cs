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
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Implementations;
using SharpDX;
using static Imlight.Common.Caches.TypeCache;
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
    private const uint VolumeTemplateId = 1700;
    private static readonly object s_lockObject = new();
    private static readonly List<string> s_blacklistedObjectActives = new()
    {
        "EditorOnly",
        "PetOnly",
    };

    private static WizardZone s_zone;
    private static IActorRef s_zoneActorRef;
    private static KiWad s_wad;
    private static WizZoneData s_zoneData;
    private static SpawnManager s_spawnData;
    private static PathManager_PathTemplateList s_pathData;
    private static PathManager_NodeTemplateList s_nodeData;
    private static WizZoneVolumes s_zoneVolumes;
    private static WizZoneTriggers s_zoneTriggers;

    /// <summary>
    /// Loads the <see cref="WizardZone" /> data from the <see cref="ResourceManager" />.
    /// </summary>
    /// <param name="zone">The zone object.</param>
    /// <param name="zoneActorRef">The Akka.NET actor reference of the zone.</param>
    public static void LoadZoneData(WizardZone zone, IActorRef zoneActorRef) {
        lock (s_lockObject) {
            try {
                s_zone = zone;
                s_zoneActorRef = zoneActorRef;

                if (!ResourceManager.TryLoadFile(zone.ZoneName, out s_wad)) {
                    Logger.Error("Zone {ZoneName} tried to load its own data, but none was " +
                                 "found in the {Name}. We will continue, but the zone will not " +
                                 "contain any objects, mobs or volumes.",
                        Logger.Args(zone.ZoneName, nameof(ResourceManager)));
                    return;
                }

                var benchmarkTimer = new System.Diagnostics.Stopwatch();
                var zoneName = zone.ZoneName;
                benchmarkTimer.Start();

                LoadZoneData();
                Logger.Debug("{0} Loaded zone data in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                LoadSpawnData();
                Logger.Debug("{0} Loaded spawn data in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                LoadPathData();
                Logger.Debug("{0} Loaded path data in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                LoadNodeData();
                Logger.Debug("{0} Loaded node data in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                LoadVolumeData();
                Logger.Debug("{0} Loaded volume data in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                LoadTriggerData();
                Logger.Debug("{0} Loaded trigger data in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                CreateZoneCoreObjects();
                Logger.Debug("{0} Created core objects in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                CreateZoneCombatSigils();
                Logger.Debug("{0} Created combat sigils in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                CreateZonePaths();
                Logger.Debug("{0} Created paths in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                CreateZoneVolumes();
                Logger.Debug("{0} Created volumes in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));
                benchmarkTimer.Restart();

                CreateZoneTriggers();
                Logger.Debug("{0} Created triggers in {Time}ms.",
                    Logger.Args(zoneName, benchmarkTimer.ElapsedMilliseconds));

                benchmarkTimer.Stop();

                ClearUnmanagedMemory();
            }
            catch (Exception ex) {
                Logger.Error("An error occurred while loading zone data: {ErrorMessage} {StackTrace}",
                    Logger.Args(ex.Message, ex.StackTrace));
            }
        }
    }

    /// <summary>
    /// Loads the zone data from the KIWAD file.
    /// </summary>
    private static void LoadZoneData() {
        Logger.Verbose("Loading zone data for {ZoneName}...", Logger.Args(s_zone.ZoneName));

        var serializer = new FileSerializer();
        s_zoneData = serializer.OpenClass<WizZoneData>(s_wad, ZoneDataFileName);
        s_zone.ZoneDisplayName = s_zoneData.m_zoneDisplayName;

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
        s_spawnData = serializer.OpenClass<SpawnManager>(s_wad, SpawnDataFileName);

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
        s_pathData = serializer.OpenClass<PathManager_PathTemplateList>(s_wad, PathDataFileName);

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
        s_nodeData = serializer.OpenClass<PathManager_NodeTemplateList>(s_wad, NodeDataFileName);

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
            if (objectInfo.m_spawnRequirements is not null) {
                var requirements = objectInfo.m_spawnRequirements.m_requirements?.ToList();
                var operatorType = objectInfo.m_spawnRequirements.m_operator;
                if (!CheckGlobalRegistryRequirements(requirements, operatorType)) {
                    continue;
                }
            }

            var template = (GameObjectTemplate) CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            var newObject = CoreObjectFactory.FinalizeCoreObject(objectInfo, template);
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
            var template = (GameObjectTemplate) CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            var newObject = CoreObjectFactory.FinalizeCoreObject(objectInfo, template);
            if (newObject == null) {
                continue;
            }

            // Clipping happens sometimes. Increaase the Z-axis by 1 to prevent this.
            newObject.m_location.Z += 1;

            var message = new ZONE_102_PROTOCOL.MSG_ADDCOMBATSIGIL {
                CoreObject = newObject,
                Template = template
            };
            s_zoneActorRef.Tell(message);
        }
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
            // We have to use this explicit method because the volume has two `m_templateID` fields, but only the duplicate one is used.
            var newObj = CoreObjectFactory.FinalizeCoreObject(volume, volume.m_templateID);
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
                var resultList = new ResultList {
                    m_results = new List<TypeCache.Result> {
                        persistentTriggerData.Teleport
                    }
                };
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
    private static List<NodeObject> GetNodesForPath(PathObjectTemplate path) {
        var nodeList = new List<NodeObject>();
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
    private static List<SpawnObject> GetCreaturesForPath(PathObjectTemplate path) {
        var creatureList = new List<SpawnObject>();
        foreach (var spawnObject in s_spawnData.m_spawners) {
            var spawnList = spawnObject.m_spawnList;
            if (spawnList is null || spawnList.Count <= 0 || spawnList[0]?.m_objectInfo is null) {
                continue;
            }
            if (spawnList[0].m_objectInfo.m_pathID != path.m_id) {
                continue;
            }

            // Check the spawn requirements for this mob, if they exist.
            if (spawnObject.m_globalDynamicReqs is not null) {
                var requirements = spawnObject.m_globalDynamicReqs.m_requirements.ToList();
                var operatorType = spawnObject.m_globalDynamicReqs.m_operator;
                if (!CheckGlobalRegistryRequirements(requirements, operatorType)) {
                    continue;
                }
            }

            // If the Id matches and all the requirements are met, add it to the list.
            creatureList.Add(spawnObject);

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
    /// Checks if the global registry requirements are met.
    /// </summary>
    /// <param name="values">The list of requirements to check.</param>
    /// <param name="operatorType">The operator type to use for combining the requirements.</param>
    /// <returns>True if all requirements are met, false otherwise.</returns>
    private static bool CheckGlobalRegistryRequirements(List<Requirement> values, Requirement.Operator operatorType) {
        var allMatched = operatorType == Requirement.Operator.ROP_OR;

        foreach (var requirement in values) {
            if (requirement is ReqGlobalRegistryValue globalReq) {
                var globalValueMet = GlobalRegistryValueMet(globalReq);
                if (globalValueMet) {
                    continue;
                }
                else {
                    allMatched = allMatched && !globalReq.m_applyNOT;
                }
            }
            else {
                Logger.Warning("Holy!!! We found a spawn requirement that isn't a global registry value. " +
                            "This is a problem. Let Jooty know.");
            }
        }

        return allMatched;
    }

    /// <summary>
    /// Checks if the given global registry value meets the specified condition.
    /// </summary>
    /// <param name="value">The requirement for the global registry value.</param>
    /// <returns>True if the global registry value meets the condition, false otherwise.</returns>
    private static bool GlobalRegistryValueMet(ReqGlobalRegistryValue value) {
        var globalValue = GlobalRegistryCollection.GetRegistryEntry(value.m_entryName);

        switch (value.m_operatorType) {
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_EQUALS:
                return value.m_numericValue == globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_LESS_THAN:
                return value.m_numericValue < globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_LESS_THAN_EQ:
                return value.m_numericValue <= globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_GREATER_THAN:
                return value.m_numericValue > globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ:
                return value.m_numericValue >= globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_UNKNOWN:
            default: {
                    Logger.Error("Zone {ZoneName} contains a spawn requirement that " +
                                      "references a global registry value that does not exist. " +
                                      "Entry name: {EntryName}", Logger.Args(s_zone.ZoneName, value.m_entryName));
                    return false;
                }
        }
    }

    /// <summary>
    /// Checks to see if all creatures in a <see cref="SpawnItem"/> contain the same path ID.
    /// </summary>
    /// <param name="spawnList">The list of spawns.</param>
    /// <returns>True, if all the creatures are on the same path; false otherwise.</returns>
    private static bool AllObjectsContainSamePath(IReadOnlyList<SpawnItem> spawnList) {
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
