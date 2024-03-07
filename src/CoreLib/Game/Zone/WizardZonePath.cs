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
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using static Imlight.Common.Caches.TypeCache;

#pragma warning disable CS4014

namespace Imlight.CoreLib.Game.Zone;

/// <summary>
/// This is a child actor of a <see cref="WizardZoneObjectSupervisor" /> that represents a path that exists in that zone. It is also
/// responsible for spawning the creatures on interval.
/// </summary>
public class WizardZonePath : ReceiveProtocolDispatcher, IWithTimers {
    private const int SPAWN_INTERVAL_WARNING_THRESHOLD = 2;
    private const int MAX_SPAWNS_ALLOWED = 25;
    private const int SPAWN_INTERVAL_START_DELAY = 5;

    public GID Id { get; init; }
    public ByteString Name { get; init; }
    public ITimerScheduler Timers { get; set; }
    public readonly Dictionary<NodeObject, bool> Nodes;

    private readonly TimeSpan _spawnIntervalStartDelay = TimeSpan.FromSeconds(SPAWN_INTERVAL_START_DELAY);
    private readonly List<IActorRef> _creatures;
    private readonly Dictionary<SpawnObject, byte> _creatureCount;
    private readonly List<SpawnObject> _creatureSpawnData;
    private readonly IActorRef _zoneActorRef;

    // ctor
    public WizardZonePath(
        GID id,
        ByteString name,
        List<NodeObject> nodes,
        List<SpawnObject> creatures,
        IActorRef zoneActorRef) {
        this.Id = id;
        this.Name = name;
        this.Nodes = nodes.ToDictionary(x => x, _ => true);
        this._creatures = new List<IActorRef>();
        this._creatureSpawnData = creatures;
        this._creatureCount = new Dictionary<SpawnObject, byte>();
        this._zoneActorRef = zoneActorRef;

        StartSpawnInterval();
    }

    // Akka.NET ctor
    public static Props Props(
        GID id,
        ByteString name,
        List<NodeObject> nodes,
        List<SpawnObject> creatures,
        IActorRef zoneActorRef) {
        return Akka.Actor.Props.Create(() => new WizardZonePath(id, name, nodes, creatures, zoneActorRef));
    }

    public void RemoveCreature(ulong templateId) {
        var creature = _creatureSpawnData.First(x => x.m_spawnList.Any(y => y.m_objectInfo.m_templateID == templateId));
        if (creature == null) {
            Logger.Error("Creature with template ID {0} was not found in the creature spawn data.",
                Logger.Args(templateId));
            return;
        }

        var count = CreatureCount(creature);
        if (count <= 0) {
            Logger.Error("Creature {0} has no spawns to remove.",
                Logger.Args(creature.m_name));
            return;
        }

        SetCreatureCount(creature, count - 1);
    }

    private void StartSpawnInterval() {
        foreach (var spawnObject in _creatureSpawnData.Where(x => x.m_active)) {
            _creatureCount.Add(spawnObject, 0);

            var msg = new ZONE_102_PROTOCOL.MSG_CREATURESPAWNINTERVAL { SpawnObject = spawnObject };

            var interval = TimeSpan.FromSeconds(spawnObject.m_spawnTime);
            if (interval <= TimeSpan.Zero) {
                Logger.Warning("Creature {0} has a spawn interval of 0 seconds. This is not allowed.",
                    Logger.Args(spawnObject.m_name));
                continue;
            }

            if (interval <= TimeSpan.FromSeconds(SPAWN_INTERVAL_WARNING_THRESHOLD)) {
                Logger.Warning("Creature {0} has a dangerous spawn interval of less than {1} seconds.",
                    Logger.Args(spawnObject.m_name, SPAWN_INTERVAL_WARNING_THRESHOLD));
            }

            Timers.StartPeriodicTimer("spawninterval", msg, _spawnIntervalStartDelay, interval);
        }
    }

    #region Handlers

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST message) {
        // Just forward this broadcast to each of the creatures on this path.
        foreach (var creature in _creatures) {
            foreach (var msg in message.Messages) {
                creature.Tell(msg);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATURESPAWNINTERVAL))]
    private void ReceiveCreatureSpawnInterval(ZONE_102_PROTOCOL.MSG_CREATURESPAWNINTERVAL message) {
        // Determine if we can spawn this object.
        var obj = message.SpawnObject;

        if (CanSpawn(obj)) {
            var currentCount = CreatureCount(obj);
            var spawnsAvailable = obj.m_maxNumberOfSpawns - currentCount;

            // We've determined that this object must spawn.
            if (spawnsAvailable == 0) {
                spawnsAvailable = 1;
            }

            // For every slot available, spawn a creature on interval.
            var msg = new ZONE_102_PROTOCOL.MSG_CREATURESPAWNONPATH {
                SpawnObject = obj,
                Count = (int) spawnsAvailable,
                SpawnRate = (int) obj.m_respawnRate
            };
            Self.Tell(msg);

            // Increment the creature count.
            SetCreatureCount(obj, currentCount + (int) spawnsAvailable);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_CREATURESPAWNONPATH))]
    private void ReceiveCreatureSpawnOnPath(ZONE_102_PROTOCOL.MSG_CREATURESPAWNONPATH message) {
        // This method is recursive. It will spawn a creature on the path and then call itself again to spawn another creature.
        // It will continue to do this until the count is 0.
        if (message.Count <= 0) {
            return;
        }

        if (CreatureCount(message.SpawnObject) > message.SpawnObject.m_maxNumberOfSpawns) {
            Logger.Error("Creature {0} has reached the maximum number of spawns allowed ({1}).",
                Logger.Args(message.SpawnObject.m_name, message.SpawnObject.m_maxNumberOfSpawns));
            return;
        }

        // Pick a random spawn object from the list and spawn it.
        var rngSpawn = PickRandomSpawnObject(message.SpawnObject.m_spawnList);
        var spawnInfo = rngSpawn.m_objectInfo;

        // The spawn info declares what type of node to spawn on.
        var spawnNode = GetRelevantNode(spawnInfo);
        var nodeIndex = Nodes.Keys.ToList().IndexOf(spawnNode);
        Nodes[spawnNode] = false;

        // Create the creature object.
        var template = CoreObjectFactory.GetCoreTemplate(spawnInfo.m_templateID);
        var newObj = CoreObjectFactory.FinalizeCoreObject(spawnInfo, template);
        newObj = CoreObjectFactory.InitializeCoreObjectBehaviors(newObj, template);
        newObj.m_location = spawnNode.m_location;

        // Create the creature actor. This will also add the creature to the zone.
        // The creature actor will be responsible for updating the zone with its presence.
        var props = WizardZoneCreature.Props(newObj, template, this, (byte) nodeIndex, _zoneActorRef);
        var actorRef = Context.ActorOf(props);
        _creatures.Add(actorRef);

        // Tell the zone about the new creature.
        var msg = new ZONE_102_PROTOCOL.MSG_ADDCREATURE {
            ObjectIdentity = actorRef,
            CoreObject = newObj
        };
        _zoneActorRef.Tell(msg);

        // Decrement the count and set a timer to spawn the next creature.
        message.Count--;
        var spawnIntervalDelay = TimeSpan.FromSeconds(message.SpawnRate);
        Timers.StartSingleTimer("spawncreature", message, spawnIntervalDelay);
    }

    #endregion

    private bool CanSpawn(SpawnObject spawnObject) {
        if (!_creatureCount.TryGetValue(spawnObject, out var count)) {
            throw new Exception("Somehow, this SpawnObject was not found in the creature count dictionary?");
        }

        if (count >= MAX_SPAWNS_ALLOWED) {
            return false;
        }

        if (count <= 0 && spawnObject.m_atLeastOneSpawn) {
            return true;
        }

        if (count >= spawnObject.m_maxNumberOfSpawns) {
            return false;
        }

        return true;
    }

    private int CreatureCount(SpawnObject spawnObject) {
        var count = _creatureCount[spawnObject];

        if (count >= MAX_SPAWNS_ALLOWED) {
            Logger.Error("Creature {0} has reached the maximum number of spawns allowed ({1}).",
                Logger.Args(spawnObject.m_name, MAX_SPAWNS_ALLOWED));
        }

        return _creatureCount[spawnObject];
    }

    private void SetCreatureCount(SpawnObject spawnObject, int count) {
        _creatureCount[spawnObject] = (byte) count;
    }

    private SpawnItem PickRandomSpawnObject(List<SpawnItem> spawnItems) {
        var rng = new Random();
        var rngNum = rng.Next(0, 100);

        var cumulativePercentage = 0;
        foreach (var t in spawnItems) {
            cumulativePercentage += t.m_percentChance;
            if (rngNum < cumulativePercentage) {
                return t;
            }
        }

        return spawnItems[^1];
    }

    private NodeObject GetRelevantNode(SpawnObjectInfo spawnInfo) {
        switch (spawnInfo.m_kStartNodeType) {
            case SpawnObjectInfo.StartNodeType.SNT_RANDOM:
                var rng = new Random();
                var rngIndex = rng.Next(0, Nodes.Count);
                return Nodes.ElementAt(rngIndex).Key;
            case TypeCache.SpawnObjectInfo.StartNodeType.SNT_RANDOM_UNIQUE:
                var selection = Nodes.Where(x => x.Value).ToArray();
                var rng2 = new Random();
                var rngIndex2 = rng2.Next(0, selection.Length);
                return Nodes.ElementAt(rngIndex2).Key;
            case TypeCache.SpawnObjectInfo.StartNodeType.SNT_FIRST:
                return Nodes.First().Key;
            case TypeCache.SpawnObjectInfo.StartNodeType.SNT_LAST:
                return Nodes.Last().Key;
            case TypeCache.SpawnObjectInfo.StartNodeType.SNT_SPECIFIC:
                return Nodes.FirstOrDefault().Key;
            default:
                throw new ArgumentOutOfRangeException(nameof(spawnInfo.m_kStartNodeType),
                    spawnInfo.m_kStartNodeType,
                    "Invalid StartNodeType value");
        }
    }
}
