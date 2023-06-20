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

namespace Imlight.Server.Game.Zone;

/// <summary>
///     A static, thread-safe class for loading zone data from the <see cref="ResourceManager" />.
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

    // Unmanaged data of our current working WizardZone. This should be properly disposed of when not working.
    private static WizardZone _zone;
    private static IActorRef _zoneActorRef;
    private static IActorContext _zoneActorContext;
    private static Wad _wad;
    private static WizZoneData _zoneData;
    private static SpawnManager _spawnData;
    private static PathManager_PathTemplateList _pathData;
    private static PathManager_NodeTemplateList _nodeData;

    /// <summary>
    /// Loads the <see cref="WizardZone" /> from the <see cref="ResourceManager" />. The name of the zone will be used.
    /// </summary>
    /// <param name="zone">The zone object.</param>
    /// <param name="zoneActorRef">The Akka.NET actor reference of the zone. This is used to spawn zone objects.</param>
    /// <param name="zoneActorContext">The Akka.NET actor of the zone. This is used to spawn child actors.</param>
    public static void LoadZoneData(WizardZone zone, IActorRef zoneActorRef, IActorContext zoneActorContext)
    {
        lock (LockObject)
        {
            try
            {
                _zone = zone;
                _zoneActorRef = zoneActorRef;
                _zoneActorContext = zoneActorContext;

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

    private static void LoadZoneData()
    {
        var serializer = new FileSerializer();
        _zoneData = serializer.OpenClass<WizZoneData>(_wad, ZoneDataFileName);
        if (_zoneData is null)
            Log.Logger.Error($"Zone {_zone.ZoneName} could not load {ZoneDataFileName} was missing or invalid.");
    }

    private static void LoadSpawnData()
    {
        var serializer = new FileSerializer();
        _spawnData = serializer.OpenClass<SpawnManager>(_wad, SpawnDataFileName);
        if (_spawnData is null)
            Log.Logger.Error($"Zone {_zone.ZoneName} could not load {SpawnDataFileName} was missing or invalid.");
    }

    private static void LoadPathData()
    {
        var serializer = new FileSerializer();
        _pathData = serializer.OpenClass<PathManager_PathTemplateList>(_wad, PathDataFileName);
        if (_pathData is null)
            Log.Logger.Error($"Zone {_zone.ZoneName} could not load {PathDataFileName} was missing or invalid.");
    }

    private static void LoadNodeData()
    {
        var serializer = new FileSerializer();
        _nodeData = serializer.OpenClass<PathManager_NodeTemplateList>(_wad, NodeDataFileName);
        if (_nodeData is null)
            Log.Logger.Error($"Zone {_zone.ZoneName} could not load {NodeDataFileName} was missing or invalid.");
    }

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

    private static void CreateZonePaths()
    {
        // Iterate through each path and create our own proprietary WizardZonePath type. Create each path as a
        // child actor for this zone.
        foreach (var path in _pathData.m_pathList)
        {
            // Create the path in its entirety; add nodes to the path, then add the creature data to the path.
            var nodeList = GetNodesForPath(path);
            var creatureList = GetCreaturesForPath(path);

            // Create the WizardZonePath actor as a child of the zone actor.
            var wizPathProps = WizardZonePath.Props(path.m_id, path.m_name, nodeList, creatureList);
            CreateChildActor(wizPathProps, path.m_name.ToString().Replace(' ', '_'));
        }
    }

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

    private static bool AllObjectsContainSamePath(IReadOnlyList<SpawnItem> spawnList)
    {
        var firstPathId = spawnList[0].m_objectInfo.m_pathID;
        return spawnList.All(x => x.m_objectInfo.m_pathID == firstPathId);
    }

    private static void CreateChildActor(Props props, string childActorName)
    {
        _zoneActorContext.ActorOf(props, childActorName);
    }

    private static void ClearUnmanagedMemory()
    {
        _zone = null;
        _zoneActorRef = null;
        _wad = null;
        _zoneData = null;
        _spawnData = null;
        _pathData = null;
        _nodeData = null;
    }
}