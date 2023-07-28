/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Server.Database;
using Imlight.Server.Shared.Packets;
using WizUnraveler.IO;
using WizUnraveler.ObjectProperty;
using static WizUnraveler.Cache.TypeCache;

#pragma warning disable CS4014

namespace Imlight.Server.Game.Zone;

/// <summary>
/// This is a child actor of a <see cref="WizardZone" /> that represents a path that exists in that zone. It is also
/// responsible for spawning the creatures on interval.
/// </summary>
public class WizardZonePath : ReceiveActor
{
    /// <summary>
    /// Gets the identifier of the path.
    /// </summary>
    public GID Id { get; init; }

    /// <summary>
    /// Gets the name of the path.
    /// </summary>
    public ByteString Name { get; init; }
    
    private readonly CancellationTokenSource _cancelToken;
    private readonly Dictionary<GID, byte> _creatureCount;
    private readonly List<SpawnObject> _creatureSpawnData;

    // The the value represents if the NodeObject is available.
    private readonly Dictionary<NodeObject, bool> _nodes;
    private readonly IActorRef _zoneActorRef;

    // ctor
    /// <summary>
    /// Initializes a new instance of the <see cref="WizardZonePath" /> class.
    /// </summary>
    /// <param name="id">The identifier of the path.</param>
    /// <param name="name">The name of the path.</param>
    /// <param name="nodes">The list of nodes.</param>
    /// <param name="creatures">The list of creatures.</param>
    /// <param name="zoneActorRef">The reference to the zone actor.</param>
    public WizardZonePath(
        GID id,
        ByteString name,
        List<NodeObject> nodes,
        List<SpawnObject> creatures,
        IActorRef zoneActorRef)
    {
        Id = id;
        Name = name;
        _nodes = nodes.ToDictionary(x => x, _ => true);
        _creatureSpawnData = creatures;
        _zoneActorRef = zoneActorRef;
        _cancelToken = new CancellationTokenSource();
        _creatureCount = new Dictionary<GID, byte>();

        StartSpawnInterval();
    }

    // Akka.NET ctor
    /// <summary>
    /// Creates the <see cref="Props" /> for creating a new instance of the <see cref="WizardZonePath" />.
    /// </summary>
    /// <param name="id">The identifier of the path.</param>
    /// <param name="name">The name of the path.</param>
    /// <param name="nodes">The list of nodes.</param>
    /// <param name="creatures">The list of creatures.</param>
    /// <param name="zoneActorRef">The reference to the zone actor.</param>
    /// <returns>The <see cref="Props" /> for creating the actor.</returns>
    public static Props Props(
        GID id,
        ByteString name,
        List<NodeObject> nodes,
        List<SpawnObject> creatures,
        IActorRef zoneActorRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZonePath(id, name, nodes, creatures, zoneActorRef));
    }

    /// <summary>
    /// Starts the interval for spawning creatures on the path.
    /// </summary>
    private void StartSpawnInterval()
    {
        foreach (var spawnObject in _creatureSpawnData.Where(x => x.m_active)) 
            DoCreatureSpawnInterval(spawnObject);
    }

    /// <summary>
    /// Performs the creature spawning at a specified interval.
    /// </summary>
    /// <param name="spawnObject">The spawn object containing information about the creature to be spawned.</param>
    private async Task DoCreatureSpawnInterval(SpawnObject spawnObject)
    {
        _creatureCount.Add(spawnObject.m_id, 0);

        SpawnAllCreatures(spawnObject);

        var hasRun = false;
        while (!_cancelToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(spawnObject.m_spawnTime);
            if (!CanSpawn(spawnObject) && hasRun)
            {
                var tinyDelay = TimeSpan.FromSeconds(spawnObject.m_respawnRate);
                await Task.Delay(tinyDelay);
                continue;
            }

            var rngSpawn = PickRandomSpawnObject(spawnObject.m_spawnList);
            SpawnCreature(rngSpawn.m_objectInfo);
            IncrementCreatureCount(spawnObject);

            hasRun = true;
            await Task.Delay(delay);
        }
    }

    /// <summary>
    /// Checks if a creature can be spawned based on spawn restrictions.
    /// </summary>
    /// <param name="spawnObject">The spawn object representing the creature.</param>
    /// <returns><c>true</c> if a creature can be spawned, <c>false</c> otherwise.</returns>
    private bool CanSpawn(SpawnObject spawnObject)
    {
        if (!_creatureCount.TryGetValue(spawnObject.m_id, out var count))
            throw new Exception("Somehow, this SpawnObject was not found in the creature count dictionary?");
        if (count <= 0 && spawnObject.m_atLeastOneSpawn)
            return true;
        if (count >= spawnObject.m_maxNumberOfSpawns)
            return false;

        return true;
        // TODO: Add global spawn requirements here.
        // TODO: Add population sensitive here.
    }

    /// <summary>
    /// Picks a random spawn object from a list based on their percentage chance.
    /// </summary>
    /// <param name="spawnItems">The list of spawn items to choose from.</param>
    /// <returns>The selected spawn item.</returns>
    private SpawnItem PickRandomSpawnObject(List<SpawnItem> spawnItems)
    {
        var rng = new Random();
        var rngNum = rng.Next(0, 100);

        var cumulativePercentage = 0;
        foreach (var t in spawnItems)
        {
            cumulativePercentage += t.m_percentChance;
            if (rngNum < cumulativePercentage)
                return t;
        }

        return spawnItems[^1];
    }

    /// <summary>
    /// Spawns a creature based on the given spawn object information.
    /// </summary>
    /// <param name="spawnInfo">The spawn object information of the creature to be spawned.</param>
    private void SpawnCreature(SpawnObjectInfo spawnInfo)
    {
        var spawnNode = GetRelevantNode(spawnInfo);
        var nodeIndex = _nodes.Keys.ToList().IndexOf(spawnNode);
        _nodes[spawnNode] = false;

        var newObj = CoreObjectFactory.CreateObjectFromInfo(spawnInfo);
        if (newObj is null)
            throw new NullReferenceException();
        newObj.m_location = spawnNode.m_location;

        var nodes = _nodes.Keys.ToArray();
        var props = WizardZonePathCreature.Props(newObj, nodes, (byte)nodeIndex, _zoneActorRef);
        var actorRef = Context.ActorOf(props);

        var msg = new ZONE_102_PROTOCOL.MSG_ADDCREATURE
        {
            ObjectIdentity = actorRef,
            CoreObject = newObj
        };
        _zoneActorRef.Tell(msg);
    }

    /// <summary>
    /// Spawns all creatures associated with a spawn object.
    /// </summary>
    /// <param name="spawnObject">The spawn object representing the creatures.</param>
    private void SpawnAllCreatures(SpawnObject spawnObject)
    {
        foreach (var spawn in spawnObject.m_spawnList) SpawnCreature(spawn.m_objectInfo);
    }

    /// <summary>
    /// Retrieves the relevant node for spawning a creature based on the spawn object information.
    /// </summary>
    /// <param name="spawnInfo">The spawn object information.</param>
    /// <returns>The relevant node for spawning.</returns>
    private NodeObject GetRelevantNode(SpawnObjectInfo spawnInfo)
    {
        switch (spawnInfo.m_kStartNodeType)
        {
            case SpawnObjectInfo.StartNodeType.SNT_RANDOM:
                var rng = new Random();
                var rngIndex = rng.Next(0, _nodes.Count);
                return _nodes.ElementAt(rngIndex).Key;
            case SpawnObjectInfo.StartNodeType.SNT_RANDOM_UNIQUE:
                var selection = _nodes.Where(x => x.Value).ToArray();
                var rng2 = new Random();
                var rngIndex2 = rng2.Next(0, selection.Length);
                return _nodes.ElementAt(rngIndex2).Key;
            case SpawnObjectInfo.StartNodeType.SNT_FIRST:
                return _nodes.First().Key;
            case SpawnObjectInfo.StartNodeType.SNT_LAST:
                return _nodes.Last().Key;
            case SpawnObjectInfo.StartNodeType.SNT_SPECIFIC:
                return _nodes.FirstOrDefault().Key;
            default:
                throw new ArgumentOutOfRangeException(nameof(spawnInfo.m_kStartNodeType),
                    spawnInfo.m_kStartNodeType,
                    "Invalid StartNodeType value");
        }
    }

    /// <summary>
    /// Increments the creature count for a spawn object.
    /// </summary>
    /// <param name="spawnObject">The spawn object.</param>
    private void IncrementCreatureCount(SpawnObject spawnObject)
    {
        _creatureCount[spawnObject.m_id]++;
    }
}