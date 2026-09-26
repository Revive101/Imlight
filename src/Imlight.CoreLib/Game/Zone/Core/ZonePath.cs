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
 *
 * ========================================================================
 * PATH AND SPAWN MANAGEMENT SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Manages entity spawn points and movement paths within a zone, handling
 * creature creation, path following, and respawn mechanics.
 * 
 * USAGE EXAMPLE:
 * // Created by the Zone system during zone loading
 * // PathObjectTemplate template = ...
 * // List<NodeObject> nodes = ...
 * // List<SpawnObject> creatures = ...
 * var pathActor = Context.ActorOf(Props.Create(() => 
 *     new ZonePath(template, nodes, creatures, zoneRef, zone)));
 * 
 * NOTE:
 * Handles timed creature spawning based on configuration in SpawnObjects.
 * Enforces spawn limits and ensures proper creature distribution.
 * Uses timer-based respawn intervals for continuous population management.
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/26/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.MessageLayer.Generated;
using Imcodec.Types;

namespace Imlight.CoreLib.Game.Zone.Core;

/// <summary>
/// Manages a number of <see cref="ZoneEntity"/>s that exist in the game world following a path.
/// </summary>
/// <param name="template">The template of the path.</param>
/// <param name="nodes">The nodes of the path.</param>
/// <param name="creatures">The creatures that follow the path.</param>
/// <param name="zoneRef">The reference to the zone actor.</param>
/// <param name="zone">The zone that the path is in.</param>
public sealed class ZonePath : ZoneEntity {

    private const string CREATURE_SPAWN_INTERVAL_LOCK = "CREATURE_SPAWN_INTERVAL_LOCK";
    private const uint INITIAL_SPAWN_DELAY_IN_SECONDS = 5;
    private const uint MAX_SPAWNS_ALLOWED = 25;
    private const uint OBJECT_CREATION_TIMEOUT_IN_MS = 5000;

    private readonly PathObjectTemplate _template;
    private readonly List<NodeObject> _nodes;
    private readonly List<SpawnObject> _creatures;
    private readonly Dictionary<SpawnObject, byte> _creatureCount = [];
    private readonly List<IActorRef> _creatureActors = [];
    private readonly Dictionary<ulong, SpawnObject> _spawnObjectInfo = [];
    private readonly Dictionary<IActorRef, (SpawnObject Spawner, GID ObjectId, string Name)> _loadingCreatures = [];
    private readonly bool _randomizeCreatures 
        = ConfigurationManager.Settings["April Fools.RandomizeCreatures"].AsBool();

    // ctor
    public ZonePath(PathObjectTemplate template, List<NodeObject> nodes, List<SpawnObject> creatures, IActorRef zoneRef, Zone zone)
        : base(null, null, null, zoneRef, zone) {
        this._template = template;
        this._nodes = nodes;
        this._creatures = creatures;
        base.ZoneRef = zoneRef;
        base.Zone = zone;

        // Begin the creature spawn interval.
        for (var i = 0; i < creatures.Count; i++) {
            var spawnObject = creatures[i];
            _creatureCount.Add(spawnObject, 0);

            if (!spawnObject.m_active) {
                continue;
            }

            var timerKey = SpawnTimerKey(spawnObject, i);
            var msg = new ZONE_102_PROTOCOL.MSG_PATHSPAWNINTERVAL { SpawnObject = spawnObject };
            var interval = TimeSpan.FromSeconds(spawnObject.m_spawnTime);

            // If the interval is 0 or below, this creature only spawns once.
            if (interval <= TimeSpan.Zero) {
                Timers.StartSingleTimer(timerKey, msg, TimeSpan.Zero);

                continue;
            }

            // Otherwise, start the interval.
            var delay = TimeSpan.FromSeconds(INITIAL_SPAWN_DELAY_IN_SECONDS);
            Timers.StartPeriodicTimer(timerKey, msg, delay, interval);
        }
    }

    private static string SpawnTimerKey(SpawnObject spawnObject, int index) {
        // Spawners with a zone level range are alternatives (one boss per level band) and share one timer, so
        // the last of them runs. todo: pick the band from the zone's level once zones carry one.
        var isLevelVariant = spawnObject.m_zoneLevelMin != 0 || spawnObject.m_zoneLevelMax != 0;

        return isLevelVariant ? CREATURE_SPAWN_INTERVAL_LOCK : $"{CREATURE_SPAWN_INTERVAL_LOCK}_{index}";
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN))]
    protected override void ReceiveObjectLoadBegin()
        => Sender.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS());

    [MessageHandler(typeof(IServerMessage))]
    protected override void ReceiveElse(IServerMessage message) {
        if (message is ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS or ZONE_102_PROTOCOL.MSG_ENTITYLOADTIMEOUT) {
            return;
        }

        // ZonePath does not have any components. Instead, it manages the creatures that follow the path.
        // Dispatch the message to all of the creatures that follow the path.
        foreach (var actor in _creatureActors) {
            actor.Forward(message);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADRESULTS))]
    private void ReceiveCreatureLoaded() {
        if (_loadingCreatures.Remove(Sender)) {
            Timers.Cancel(Sender);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ENTITYLOADTIMEOUT))]
    private void ReceiveCreatureLoadTimeout(ZONE_102_PROTOCOL.MSG_ENTITYLOADTIMEOUT message) {
        if (!_loadingCreatures.Remove(message.Entity, out var creature)) {
            return;
        }

        Logger.Error("Failed to create entity actor for {Kind} {Name} (no load reply within {Timeout} ms).",
            Logger.Args(nameof(CoreTemplate), creature.Name, OBJECT_CREATION_TIMEOUT_IN_MS));

        _creatureActors.Remove(message.Entity);
        _spawnObjectInfo.Remove(creature.ObjectId);
        SetCreatureCount(creature.Spawner, CreatureCount(creature.Spawner) - 1);
        Context.Stop(message.Entity);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_DELETEOBJECT))]
    private void ReceiveZoneBroadcast(GAME_5_PROTOCOL.MSG_DELETEOBJECT message) {
        if (_spawnObjectInfo.TryGetValue(message.GameObjectID, out var spawnObject)) {
            var count = CreatureCount(spawnObject);
            SetCreatureCount(spawnObject, count - 1);

            _spawnObjectInfo.Remove(message.GameObjectID);
            _creatureActors.RemoveAll(x => x == Sender);
            if (_loadingCreatures.Remove(Sender)) {
                Timers.Cancel(Sender);
            }
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PATHSPAWNINTERVAL))]
    private void ReceiveCreatureSpawnInterval(ZONE_102_PROTOCOL.MSG_PATHSPAWNINTERVAL message) 
        => HandleCreatureSpawn(message.SpawnObject);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEPATHSPAWN))]
    private void ReceiveCreatureSpawn(ZONE_102_PROTOCOL.MSG_ZONEPATHSPAWN message) {
        var spawnObject = _creatures.FirstOrDefault(x => x.m_id == message.SpawnObjectID);
        if (spawnObject != null) {
            HandleCreatureSpawn(spawnObject);
        }
    }

    private void HandleCreatureSpawn(SpawnObject spawnObject) {
        // Determine if the conditions match to spawn the objects.
        if (!CanSpawn(spawnObject)) {
            return;
        }

        // We meet the conditions to spawn whatever is within this spawn object.
        // The name is confusing, but a SpawnObject may contain different creatures to spawn.
        // Choose a random creature to spawn from the SpawnObject.
        var spawnItemInfo = PickRandomSpawnObject(spawnObject.m_spawnList).m_objectInfo;

        // Get the node to spawn the creature at.
        var spawnNode = GetRelevantNode(spawnItemInfo);

        // If creatures are randomized, pick a random template ID.
        if (_randomizeCreatures) {
            spawnItemInfo.m_templateID = AprilFools.CreatureList.GetRandomCreatureTemplateID();
        }

        // Create the creature using the data we have.
        var template = CoreObjectFactory.GetCoreTemplate(spawnItemInfo.m_templateID);
        if (template is null) {
            Logger.Error("Failed to get template for {0}.",
                Logger.Args(spawnItemInfo.m_templateID));

            return;
        }

        var creatureObj = CoreObjectFactory.FinalizeCoreObject(spawnItemInfo, template);
        creatureObj = CoreObjectFactory.InitializeCoreObjectBehaviors(creatureObj, template);
        creatureObj.m_location = spawnNode.m_location;
        var Z = (float) (spawnNode.m_direction * (Math.PI / 180));
        creatureObj.m_orientation = new Imcodec.Math.Vector3(
            creatureObj.m_orientation.X,
            creatureObj.m_orientation.Y,
            Z
        );

        // Create the creature actor. It counts toward the spawner's limit while it loads; a load that never
        // answers gives the slot back.
        var actorName = CreateEntityActorName(creatureObj);
        var creatureActor = Context.ActorOf(Props.Create(() => new ZoneEntity(creatureObj, template, null, ZoneRef, Zone)), actorName);
        creatureActor.Tell(new ZONE_102_PROTOCOL.MSG_ZONEOBJECTLOADBEGIN());
        _loadingCreatures[creatureActor] = (spawnObject, creatureObj.m_globalID, (template as GameObjectTemplate)?.m_objectName);
        Timers.StartSingleTimer(creatureActor, new ZONE_102_PROTOCOL.MSG_ENTITYLOADTIMEOUT { Entity = creatureActor },
            TimeSpan.FromMilliseconds(OBJECT_CREATION_TIMEOUT_IN_MS));

        _creatureActors.Add(creatureActor);
        _spawnObjectInfo.Add(creatureObj.m_globalID, spawnObject);

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
            case StartNodeType.SNT_RANDOM:
                var rng = new Random();
                var rngIndex = rng.Next(0, _nodes.Count);
                return _nodes.ElementAt(rngIndex);
            case StartNodeType.SNT_RANDOM_UNIQUE:
                var rng2 = new Random();
                var rngIndex2 = rng2.Next(0, _nodes.Count);
                return _nodes.ElementAt(rngIndex2);
            case StartNodeType.SNT_FIRST:
                return _nodes.First();
            case StartNodeType.SNT_LAST:
                return _nodes.Last();
            case StartNodeType.SNT_SPECIFIC:
                return _nodes.FirstOrDefault();
            default:
                throw new ArgumentOutOfRangeException(nameof(spawnInfo.m_kStartNodeType),
                    spawnInfo.m_kStartNodeType,
                    "Invalid StartNodeType value");
        }
    }

    private static string CreateEntityActorName(CoreObject coreObject) {
        var actorName = $"{coreObject.m_debugName}_{coreObject.m_globalID.Full}";

        // Only alphanumeric characters and underscores are allowed in actor names.
        actorName = new string([.. actorName.Where(c => char.IsLetterOrDigit(c) || c == '_')]);

        return actorName;
    }

}