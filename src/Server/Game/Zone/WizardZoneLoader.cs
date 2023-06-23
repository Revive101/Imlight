/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common.Utilities;
using Imlight.Server.Database;
using Imlight.Server.Shared.Packets;
using WizUnraveler.Formats;
using WizUnraveler.IO;
using static WizUnraveler.Cache.TypeCache;
using static WizUnraveler.ObjectProperty.ObjectSerializer;

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
    private static readonly object LockObject = new();

    private static WizardZone _zone;
    private static IActorRef _zoneActorRef;
    private static Wad _wad;
    private static WizZoneData _zoneData;
    private static SpawnManager _spawnData;
    private static PathManager_PathTemplateList _pathData;
    private static PathManager_NodeTemplateList _nodeData;

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
                _zone = zone;
                _zoneActorRef = zoneActorRef;

                if (!ResourceManager.TryLoadFile(zone.ZoneName, out _wad))
                {
                    Log.Logger.Error($"Zone [{zone.ZoneName}] tried to load its own data, but none was " +
                                     $"found in the {nameof(ResourceManager)}. This is fine, but the zone will not " +
                                     $"contain any objects, mobs or volumes.");
                    return;
                }

                LoadZoneData();
                LoadSpawnData();
                LoadPathData();
                LoadNodeData();
                CreateZoneGameObjects();
                CreateZonePaths();
            }
            catch (Exception ex)
            {
                Log.Logger.Warning($"Zone [{zone.ZoneName}] could not load resources for whatever " +
                                   $"reason. EXCEPTION THROWN: {ex.Message}");
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
            Log.Logger.Error(
                $"Zone {_zone.ZoneName} could not load {ZoneDataFileName} as it was missing or invalid.");
    }

    /// <summary>
    /// Loads the spawn data from the KIWAD file.
    /// </summary>
    private static void LoadSpawnData()
    {
        var serializer = new FileSerializer()
            .WithSerializerFlags(SerializerFlags.UseFlags | SerializerFlags.CompactLength |
                                 SerializerFlags.StringEnums);
        _spawnData = serializer.OpenClass<SpawnManager>(_wad, SpawnDataFileName);
        if (_spawnData is null)
            Log.Logger.Error(
                $"Zone {_zone.ZoneName} could not load {SpawnDataFileName} as it was missing or invalid.");
    }

    /// <summary>
    /// Loads the path data from the KIWAD file.
    /// </summary>
    private static void LoadPathData()
    {
        var serializer = new FileSerializer();
        _pathData = serializer.OpenClass<PathManager_PathTemplateList>(_wad, PathDataFileName);
        if (_pathData is null)
            Log.Logger.Error(
                $"Zone {_zone.ZoneName} could not load {PathDataFileName} as it was missing or invalid.");
    }

    /// <summary>
    /// Loads the node data from the KIWAD file.
    /// </summary>
    private static void LoadNodeData()
    {
        var serializer = new FileSerializer();
        _nodeData = serializer.OpenClass<PathManager_NodeTemplateList>(_wad, NodeDataFileName);
        if (_nodeData is null)
            Log.Logger.Error(
                $"Zone {_zone.ZoneName} could not load {NodeDataFileName} as it was missing or invalid.");
    }

    /// <summary>
    /// Creates game objects for the zone based on the loaded zone data.
    /// </summary>
    private static void CreateZoneGameObjects()
    {
        foreach (var obj in _zoneData.m_objectList.Where(x => x is not null))
        {
            var newObj = CoreObjectFactory.CreateObjectFromInfo(obj);
            if (newObj is null)
                continue;

            var msg = new ZONE_102_PROTOCOL.MSG_ADDOBJECT { CoreObject = newObj };
            _zoneActorRef.Tell(msg);
        }
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
                Log.Logger.Warning($"Zone [{_zone.ZoneName}] contained a path [{path.m_name}] with a " +
                                   $"node that could not be found. Node ID: [{id}]");
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
                Log.Logger.Warning($"Zone [{_zone.ZoneName}] contains a SpawnObject [{spawnObject.m_name}] " +
                                   $"that contains multiple objects, which spawn on different paths. Let Jooty know.");
        }

        return creatureList;
    }
    
    /// <summary>
    /// Checks to see if all creates in a <see cref="SpawnItem"/> contain the same path ID.
    /// </summary>
    /// <param name="spawnList">The list of spawns.</param>
    /// <returns>True, if all the creatures are on the same path; false otherwise.</returns>
    private static bool AllObjectsContainSamePath(IReadOnlyList<SpawnItem> spawnList)
    {
        var firstPathId = spawnList[0].m_objectInfo.m_pathID;
        return spawnList.All(x => x.m_objectInfo.m_pathID == firstPathId);
    }

    /// <summary>
    /// Clears any unmanaged memory and resources.
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
    }
}