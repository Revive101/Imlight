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
/// This is a child actor of a <see cref="WizardZone"/> that represents a path that exists in that zone. It is also
/// responsible for spawning the creatures on interval.
/// </summary>
public class WizardZonePath : ReceiveActor
{
    public GID Id { get; init; }
    public ByteString Name { get; init; }
    
    // The the value represents if the NodeObject is available.
    private readonly Dictionary<NodeObject, bool> _nodes;
    private readonly List<SpawnObject> _creatureSpawnData;
    private readonly IActorRef _zoneActorRef;
    private readonly CancellationTokenSource _cancelToken;
    private readonly Dictionary<GID, byte> _creatureCount;

    // ctor
    public WizardZonePath(
        GID id, 
        ByteString name, 
        List<NodeObject> nodes, 
        List<SpawnObject> creatures,
        IActorRef zoneActorRef)
    {
        this.Id = id;
        this.Name = name;
        this._nodes = nodes.ToDictionary(x => x, _ => true);
        this._creatureSpawnData = creatures;
        this._zoneActorRef = zoneActorRef;
        this._cancelToken = new CancellationTokenSource();
        this._creatureCount = new Dictionary<GID, byte>();
        
        StartSpawnInterval();
    }
    
    // Akka.NET ctor
    public static Props Props(
        GID id, 
        ByteString name, 
        List<NodeObject> nodes, 
        List<SpawnObject> creatures, 
        IActorRef zoneActorRef)
    {
        return Akka.Actor.Props.Create(() => new WizardZonePath(id, name, nodes, creatures, zoneActorRef));
    }

    private void StartSpawnInterval()
    {
        // Foreach spawn data in this path, we're going to create a new asynchronous task to spawn it on interval.
        foreach (var spawnObject in _creatureSpawnData.Where(x => x.m_active))
        {
            DoCreatureSpawnInterval(spawnObject);
        }
    }

    private async Task DoCreatureSpawnInterval(SpawnObject spawnObject)
    {
        // Add the creature to the counter dictionary.
        _creatureCount.Add(spawnObject.m_id, 0);

        // When the path is just created, spawn all creatures.
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

    private bool CanSpawn(SpawnObject spawnObject)
    {
        // Check to see if we've reached the maximum amount allowed for this creature.
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
        
        // If no percentage is selected, return the last one.
        return spawnItems[^1];
    }

    private void SpawnCreature(SpawnObjectInfo spawnInfo)
    {
        var spawnNode = GetRelevantNode(spawnInfo);
        var nodeIndex = _nodes.Keys.ToList().IndexOf(spawnNode);
        _nodes[spawnNode] = false;
        
        var newObj = CoreObjectFactory.CreateObjectFromInfo(spawnInfo);
        if (newObj is null)
            throw new NullReferenceException();
        newObj.m_location = spawnNode.m_location;

        // Create the creature as a child actor of this actor.
        var nodes = _nodes.Keys.ToArray();
        var props = WizardZonePathNavigator.Props(newObj, nodes, (byte)nodeIndex, _zoneActorRef);
        var actorRef = Context.ActorOf(props);
        
        // Tell the server about the creature we just created. This will also give the creature it's own mobile ID.
        var msg = new ZONE_102_PROTOCOL.MSG_ADDCREATURE
        {
            ObjectIdentity = actorRef,
            CoreObject = newObj
        };
        _zoneActorRef.Tell(msg);
    }

    private void SpawnAllCreatures(SpawnObject spawnObject)
    {
        foreach (var spawn in spawnObject.m_spawnList)
        {
            SpawnCreature(spawn.m_objectInfo);
        }
    }

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

    private void IncrementCreatureCount(SpawnObject spawnObject)
    {
        _creatureCount[spawnObject.m_id]++;
    }
}