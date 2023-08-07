/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Data;
using Imlight.Server.Data.WizardData;
using Imlight.Server.Shared.Packets;
using SharpDX;
using WizUnraveler.Cache;
using WizUnraveler.Formats;
using WizUnraveler.IO;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.ObjectProperty.ObjectSerializer;
using static WizUnraveler.Secrets.ServerTypeCache;

namespace Imlight.Server.Game.Zone;

/// <summary>
/// A static, thread-safe class for loading zone data from the <see cref="ResourceManager" />.
/// </summary>
public static class WizardZoneLoader
{
    private const string ZoneDataFileName = "gamedata.bin";
    private const string SpawnDataFileName = "spawnData.xml";
    private const string PathDataFileName = "pathData.xml";
    private const string NodeDataFileName = "pathNodeData.bin";
    private const string VolumeDataFileName = "volumes.xml";
    private const string TriggerDataFileName = "triggers.xml";
    private const string ResultCollectionName = "zone_triggers";
    private static readonly object LockObject = new();
    private static readonly List<string> BlacklistedObjectActives = new()
    {
        "EditorOnly",
        "PetOnly",
        "Basic Positional.AdjRef",
        "Basic Linear.AdjRef"
    };

    private static WizardZone _zone;
    private static IActorRef _zoneActorRef;
    private static Wad _wad;
    private static WizZoneData _zoneData;
    private static SpawnManager _spawnData;
    private static PathManager_PathTemplateList _pathData;
    private static PathManager_NodeTemplateList _nodeData;
    private static WizZoneVolumes _zoneVolumes;
    private static WizZoneTriggers _zoneTriggers;

    /// <summary>
    /// Loads the <see cref="WizardZone" /> data from the <see cref="ResourceManager" />.
    /// </summary>
    /// <param name="zone">The zone object.</param>
    /// <param name="zoneActorRef">The Akka.NET actor reference of the zone.</param>
    public static void LoadZoneData(WizardZone zone, IActorRef zoneActorRef)
    {
        lock (LockObject)
        {
            try
            {
                var t= _zone = zone;
                _zoneActorRef = zoneActorRef;

                if (!ResourceManager.TryLoadFile(zone.ZoneName, out _wad))
                {
                    Log.Error("Zone {ZoneName} tried to load its own data, but none was " +
                                     "found in the {Name}. We will continue, but the zone will not " +
                                     "contain any objects, mobs or volumes.", 
                        Log.Args(zone.ZoneName, nameof(ResourceManager)));
                    return;
                }

                LoadZoneData();
                LoadSpawnData();
                LoadPathData();
                LoadNodeData();
                LoadVolumeData();
                LoadTriggerData();
                CreateZoneCoreObjects();
                CreateZonePaths();
                CreateZoneVolumes();
                CreateZoneTriggers();
            }
            catch (Exception ex)
            {
                Log.Warning("Zone [{ZoneName}] could not load resources for whatever " +
                                   "reason. Exception thrown: {Ex}", Log.Args(zone.ZoneName, ex));
            }
            finally
            {
                ClearUnmanagedMemory();
            }
        }
    }

    /// <summary>
    /// Loads the zone data from the KIWAD file.
    /// </summary>
    private static void LoadZoneData()
    {
        var serializer = new FileSerializer();
        _zoneData = serializer.OpenClass<WizZoneData>(_wad, ZoneDataFileName);
        if (_zoneData is null)
            Log.Error("Zone {ZoneName} could not load {ZoneDataFileName} as it was missing or invalid.",
                Log.Args(_zone.ZoneName, ZoneDataFileName));
    }

    /// <summary>
    /// Loads the spawn data from the KIWAD file.
    /// </summary>
    private static void LoadSpawnData()
    {
        var serializer = new FileSerializer()
            .WithSerializerFlags(SerializerFlags.UseFlags | SerializerFlags.CompactLength | SerializerFlags.StringEnums);
        _spawnData = serializer.OpenClass<SpawnManager>(_wad, SpawnDataFileName);
        if (_spawnData is null)
            Log.Error("Zone {Name} could not load {SpawnDataFileName} as it was missing or invalid.",
                Log.Args(_zone.ZoneName, SpawnDataFileName));
    }

    /// <summary>
    /// Loads the path data from the KIWAD file.
    /// </summary>
    private static void LoadPathData()
    {
        var serializer = new FileSerializer();
        _pathData = serializer.OpenClass<PathManager_PathTemplateList>(_wad, PathDataFileName);
        if (_pathData is null)
            Log.Error(
                "Zone {Name} could not load {PathDataFileName} as it was missing or invalid.",
                Log.Args(_zone.ZoneName, PathDataFileName));
    }

    /// <summary>
    /// Loads the node data from the KIWAD file.
    /// </summary>
    private static void LoadNodeData()
    {
        var serializer = new FileSerializer();
        _nodeData = serializer.OpenClass<PathManager_NodeTemplateList>(_wad, NodeDataFileName);
        if (_nodeData is null)
            Log.Error(
                "Zone {Name} could not load {NodeDataFileName} as it was missing or invalid.",
                Log.Args(_zone.ZoneName, NodeDataFileName));
    }

    /// <summary>
    /// Load the volume data from the KIWAD file.
    /// </summary>
    private static void LoadVolumeData()
    {
        var serializer = new FileSerializer();
        _zoneVolumes = serializer.OpenClass<WizZoneVolumes>(_wad, VolumeDataFileName);
        if (_zoneVolumes is null)
            Log.Error(
                "Zone {Name} could not load {VolumeDataFileName} as it was missing or invalid.",
                Log.Args(_zone.ZoneName, VolumeDataFileName));
    }
    
    /// <summary>
    /// Load the trigger data from the KIWAD file.
    /// </summary>
    private static void LoadTriggerData()
    {
        var serializer = new FileSerializer();
        _zoneTriggers = serializer.OpenClass<WizZoneTriggers>(_wad, TriggerDataFileName);
        if (_zoneTriggers is null)
            Log.Error(
                "Zone {Name} could not load {TriggerDataFileName} as it was missing or invalid.",
                Log.Args(_zone.ZoneName, TriggerDataFileName));
    }

    /// <summary>
    /// Creates game objects for the zone based on the loaded zone data.
    /// </summary>
    private static void CreateZoneCoreObjects()
    {
        foreach (var objectInfo in _zoneData.m_objectList.Where(info => info != null))
        {
            var template = (GameObjectTemplate)CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            var newObject = CoreObjectFactory.CreateObjectFromTemplate(objectInfo, template, objectInfo.m_templateID);
            if (newObject == null)
                continue;
            
            if (!ShouldLoadCoreObject(template))
                continue;

            var message = new ZONE_102_PROTOCOL.MSG_ADDOBJECT
            {
                CoreObject = newObject,
                Template = template
            };
            _zoneActorRef.Tell(message);
        }
    }

    /// <summary>
    /// Checks if a given object should be loaded into the zone based on zone and world events.
    /// </summary>
    /// <param name="template"></param>
    /// <returns></returns>
    private static bool ShouldLoadCoreObject(GameObjectTemplate template)
    {
        // If the object is a core object of an inactive world event, don't load it.
        if (WizardWorldData.IsCoreObjectOfInactiveWorldEvent(template))
            return false;
        if (WizardWorldData.IsCoreObjectOfInactiveZoneEvent(template, _zone.ZoneName))
            return false;
        
        // If any adjective is blacklisted, don't load the object.
        if (template.m_adjectiveList.Any(x => BlacklistedObjectActives.Contains(x)))
            return false;

        return true;
    }

    /// <summary>
    /// Creates paths for the zone based on the loaded path data.
    /// </summary>
    private static void CreateZonePaths()
    {
        foreach (var path in _pathData.m_pathList)
        {
            var nodeList = GetNodesForPath(path);
            var creatureList = GetCreaturesForPath(path);

            var msg = new ZONE_102_PROTOCOL.MSG_ADDPATH
            {
                Id = path.m_id,
                Name = path.m_name,
                Nodes = nodeList,
                Creatures = creatureList
            };
            _zoneActorRef.Tell(msg);
        }
    }

    /// <summary>
    /// Creates the volumes for the zone based on the loaded volume data.
    /// </summary>
    /// <exception cref="NullReferenceException"></exception>
    private static void CreateZoneVolumes()
    {
        if (_zoneVolumes is null) throw new NullReferenceException(nameof(_zoneVolumes));
        if (_zoneTriggers is null) throw new NullReferenceException(nameof(_zoneTriggers));
        
        foreach (var volume in _zoneVolumes.m_volumes)
        {
            var newObj = CoreObjectFactory.CreateObjectFromInfo(volume, volume.m_templateID);
            if (newObj is null)
                continue;
            
            // Set data for this CoreObject from the given volume data.
            var loc = new Vector3(volume.m_locationX, volume.m_locationY, volume.m_locationZ);
            newObj.m_location = loc;
            // For some reason, the volume type has two `m_templateID` fields, but only the duplicate one is used.
            newObj.m_templateID = volume.m_templateID; // I've never seen this templateID be anything but 1700.

            // Write a message citing the details of this volume, and send a message to the zone.
            var msg = new ZONE_102_PROTOCOL.MSG_ADDVOLUME
            {
                CoreObject = newObj,
                Volume = volume,
            };
            _zoneActorRef.Tell(msg);
        }
    }

    /// <summary>
    /// Creates the triggers for the zone based on the loaded trigger data. Trigger result data is loaded from Imlight's
    /// <see cref="ServerDataBroker"/>.
    /// </summary>
    private static void CreateZoneTriggers()
    {
        if (_zoneTriggers is null) throw new NullReferenceException(nameof(_zoneTriggers));
        
        var zoneName = _zoneData.m_zoneName;

        foreach (var trigger in _zoneTriggers.m_triggers)
        {
            // Find this trigger's results in the server database.
            var colName = SanitizeColName($"{ResultCollectionName}/{zoneName}/{trigger.m_triggerName}");
            var col = ServerDataBroker.GetCollection<TypeCache.Result>(colName);

            if (col.Any())
            {
                var resultList = new ResultList { m_results = new List<TypeCache.Result>() };
                resultList.m_results = col.ToList();
                trigger.m_results = resultList;
            }
            
            // fixme: In the grand scheme, storing a trigger without any result data is wasted space. For now, the
            // below code remains where it does for debugging purposes. In the way future, it should be added to the 
            // conditional above.

            // Write a message citing the details of this volume, and send a message to the zone.
            var msg = new ZONE_102_PROTOCOL.MSG_ADDTRIGGER { Trigger = trigger };
            _zoneActorRef.Tell(msg);
        }
    }

    /// <summary>
    /// Retrieves a list of node objects for a given path.
    /// </summary>
    /// <param name="path">The path object template.</param>
    /// <returns>A list of node objects.</returns>
    private static List<NodeObject> GetNodesForPath(PathObjectTemplate path)
    {
        var nodeList = new List<NodeObject>();
        foreach (var id in path.m_nodeIDs)
        {
            var node = _nodeData.m_nodeList.Find(n => n.m_id == id);
            if (node is not null)
                nodeList.Add(node);
            else
                Log.Warning("Zone [{Name}] contained a path {PathName} with a node that could not be found. " +
                            "Node ID: {Id}",
                    Log.Args(_zone.ZoneName, path.m_name, id));
        }

        return nodeList;
    }

    /// <summary>
    /// Retrieves a list of creature objects for a given path.
    /// </summary>
    /// <param name="path">The path object template.</param>
    /// <returns>A list of creature objects.</returns>
    private static List<SpawnObject> GetCreaturesForPath(PathObjectTemplate path)
    {
        var creatureList = new List<SpawnObject>();
        foreach (var spawnObject in _spawnData.m_spawners)
        {
            var spawnList = spawnObject.m_spawnList;
            if (spawnList is null || spawnList.Count <= 0 || spawnList[0]?.m_objectInfo is null)
                continue;

            // If the ID matches, add it to the creature list of this path.
            if (spawnList[0].m_objectInfo.m_pathID == path.m_id)
                creatureList.Add(spawnObject);

            // TEST: I'm not sure this will ever occur. Perhaps remove later?
            if (!AllObjectsContainSamePath(spawnList))
                Log.Warning("Zone {ZoneName} contains a SpawnObject {SoName} " +
                                   "that contains multiple objects, which spawn on different paths. Let Jooty know.",
                    Log.Args(_zone.ZoneName, spawnObject.m_name));
        }

        return creatureList;
    }
    
    /// <summary>
    /// Checks to see if all creatures in a <see cref="SpawnItem"/> contain the same path ID.
    /// </summary>
    /// <param name="spawnList">The list of spawns.</param>
    /// <returns>True, if all the creatures are on the same path; false otherwise.</returns>
    private static bool AllObjectsContainSamePath(IReadOnlyList<SpawnItem> spawnList)
    {
        var firstPathId = spawnList[0].m_objectInfo.m_pathID;
        return spawnList.All(x => x.m_objectInfo.m_pathID == firstPathId);
    }

    /// <summary>
    /// Clears any memory used so the next lock iteration may have a clean slate.
    /// </summary>
    private static void ClearUnmanagedMemory()
    {
        _wad = null;
        _zone = null;
        _zoneActorRef = null;
        _wad = null;
        _zoneData = null;
        _spawnData = null;
        _pathData = null;
        _nodeData = null;
        _zoneVolumes = null;
        _zoneTriggers = null;
    }
    
    private static string SanitizeColName(string colName)
    {
        // Use regular expression to remove any character that isn't an alphabet character or an underscore.
        return Regex.Replace(colName, @"[^a-zA-Z_]", "");
    }
}
