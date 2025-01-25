/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Nito.AsyncEx.Synchronous;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Manages a number of <see cref="ZoneEntity"/>s that exist in the game world following a path.
/// </summary>
/// <param name="template">The template of the path.</param>
/// <param name="nodes">The nodes of the path.</param>
/// <param name="creatures">The creatures that follow the path.</param>
/// <param name="zoneRef">The reference to the zone actor.</param>
/// <param name="zone">The zone that the path is in.</param>
public sealed class ZonePath : ZoneEntity, IWithTimers {

    private const string CREATURE_SPAWN_INTERVAL_LOCK = "CREATURE_SPAWN_INTERVAL_LOCK";
    private const uint INITIAL_SPAWN_DELAY_IN_SECONDS = 5;
    private const uint MAX_SPAWNS_ALLOWED = 25;
    private const uint OBJECT_CREATION_TIMEOUT_IN_MS = 5000;

    public ITimerScheduler Timers { get; set; }

    private readonly PathObjectTemplate _template;
    private readonly List<NodeObject> _nodes;
    private readonly List<SpawnObject> _creatures;
    private readonly Dictionary<SpawnObject, byte> _creatureCount = [];
    private readonly List<IActorRef> _creatureActors = [];

    // ctor
    public ZonePath(PathObjectTemplate template, List<NodeObject> nodes, List<SpawnObject> creatures, IActorRef zoneRef, Zone zone)
        : base(null, null, zoneRef, zone) {
        this._template = template;
        this._nodes = nodes;
        this._creatures = creatures;
        base.ZoneRef = zoneRef;
        base.Zone = zone;

        // Begin the creature spawn interval.
        foreach (var spawnObject in creatures.Where(x => x.m_active)) {
            _creatureCount.Add(spawnObject, 0);

            var msg = new ZONE_102_PROTOCOL.MSG_PATHSPAWNINTERVAL { SpawnObject = spawnObject };
            var interval = TimeSpan.FromSeconds(spawnObject.m_spawnTime);

            // If the interval is 0 or below, this creature only spawns once.
            if (interval <= TimeSpan.Zero) {
                Timers.StartSingleTimer(CREATURE_SPAWN_INTERVAL_LOCK, msg, TimeSpan.Zero);

                continue;
            }

            // Otherwise, start the interval.
            var delay = TimeSpan.FromSeconds(INITIAL_SPAWN_DELAY_IN_SECONDS);
            Timers.StartPeriodicTimer(CREATURE_SPAWN_INTERVAL_LOCK, msg, delay, interval);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected override void ReceiveObjectLoadBegin() 
        => Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS());

    [MessageHandler(typeof(IServerMessage))]
    protected override void ReceiveElse(IServerMessage message) {
        // ZonePath does not have any components. Instead, it manages the creatures that follow the path.
        // Dispatch the message to all of the creatures that follow the path.
        foreach (var actor in _creatureActors) {
            actor.Forward(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PATHSPAWNINTERVAL))]
    private void ReceiveCreatureSpawnInterval(ZONE_102_PROTOCOL.MSG_PATHSPAWNINTERVAL message) {
        // Determine if the conditions match to spawn the objects.
        var spawnObject = message.SpawnObject;
        if (!CanSpawn(spawnObject)) {
            return;
        }

        // We meet the conditions to spawn whatever is within this spawn object.
        // The name is confusing, but a SpawnObject may contain different creatures to spawn.
        // Choose a random creature to spawn from the SpawnObject.
        var spawnItemInfo = PickRandomSpawnObject(spawnObject.m_spawnList).m_objectInfo;

        // Get the node to spawn the creature at.
        var spawnNode = GetRelevantNode(spawnItemInfo);

        // Create the creature using the data we have.
        var template = CoreObjectFactory.GetCoreTemplate(spawnItemInfo.m_templateID);
        var creatureObj = CoreObjectFactory.FinalizeCoreObject(spawnItemInfo, template);
        creatureObj = CoreObjectFactory.InitializeCoreObjectBehaviors(creatureObj, template);
        creatureObj.m_location = spawnNode.m_location;

        // Create the creature actor.
        var creatureActor = CreateEntityActor(creatureObj, template);
        _creatureActors.Add(creatureActor);

        // Inform the newly created creature actor about the nodes they must walk through,
        // if relevant.
        var msg = new ZONE_102_PROTOCOL.MSG_PATHDETAILS { NodeObjects = _nodes };
        creatureActor.Tell(msg);

        // Increment the creature count.
        SetCreatureCount(spawnObject, CreatureCount(spawnObject) + 1);
    }

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

        var currentCount = CreatureCount(spawnObject);
        var spawnsAvailable = spawnObject.m_maxNumberOfSpawns - currentCount;
        if (spawnsAvailable <= 0) {
            return false;
        }

        return true;
    }

    private static SpawnItem PickRandomSpawnObject(List<SpawnItem> spawnItems) {
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

    private int CreatureCount(SpawnObject spawnObject) {
        var count = _creatureCount[spawnObject];

        if (count >= MAX_SPAWNS_ALLOWED) {
            Logger.Error("Creature {0} has reached the maximum number of spawns allowed ({1}).",
                Logger.Args(spawnObject.m_name, MAX_SPAWNS_ALLOWED));
        }

        return _creatureCount[spawnObject];
    }

    private void SetCreatureCount(SpawnObject spawnObject, int count)
        => _creatureCount[spawnObject] = (byte) count;

    private NodeObject GetRelevantNode(SpawnObjectInfo spawnInfo) {
        switch (spawnInfo.m_kStartNodeType) {
            case SpawnObjectInfo.StartNodeType.SNT_RANDOM:
                var rng = new Random();
                var rngIndex = rng.Next(0, _nodes.Count);
                return _nodes.ElementAt(rngIndex);
            case SpawnObjectInfo.StartNodeType.SNT_RANDOM_UNIQUE:
                var rng2 = new Random();
                var rngIndex2 = rng2.Next(0, _nodes.Count);
                return _nodes.ElementAt(rngIndex2);
            case SpawnObjectInfo.StartNodeType.SNT_FIRST:
                return _nodes.First();
            case SpawnObjectInfo.StartNodeType.SNT_LAST:
                return _nodes.Last();
            case SpawnObjectInfo.StartNodeType.SNT_SPECIFIC:
                return _nodes.FirstOrDefault();
            default:
                throw new ArgumentOutOfRangeException(nameof(spawnInfo.m_kStartNodeType),
                    spawnInfo.m_kStartNodeType,
                    "Invalid StartNodeType value");
        }
    }

    private IActorRef CreateEntityActor(CoreObject coreObject, CoreTemplate template) {
        var objectActor = Context.ActorOf(Props.Create(() => new ZoneEntity(coreObject, template, ZoneRef, Zone)));

        try {
            // Send a message to the object and await a reply to ensure it has been created and initialized successfully.
            var msg = new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN();
            var timeout = TimeSpan.FromMilliseconds(OBJECT_CREATION_TIMEOUT_IN_MS);
            var result = objectActor.Ask<ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS>(msg, timeout).WaitAndUnwrapException();
        }
        catch (Exception ex) {
            Logger.Error("Failed to create entity actor for {0} {1} ({2}).",
                Logger.Args(nameof(CoreTemplate), template.GetType().Name, ex.Message));

            return null;
        }

        return objectActor;
    }

}