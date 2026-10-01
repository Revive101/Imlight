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
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Requirements;
using Imlight.CoreLib.Game.Requirements.Contexts;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor
/// for any triggers that may happen with the zone.
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
/// <remarks>Triggers are any event that can happen within a zone. Zone transfers, POI
/// text, etc.</remarks>
internal sealed class ZoneTriggerSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    private static readonly bool s_randomizeGateways 
        = ConfigurationManager.Settings["April Fools.RandomizeGateways"].AsBool();

    // A trigger's own object is hidden with this state when the trigger is deactivated.
    private const string DEACTIVATED_OBJECT_STATE = "Off";

    private readonly List<(Trigger Trigger, IActorRef Actor)> _orderedTriggers = [];
    private readonly HashSet<string> _undecodableLogged = [];
    private readonly HashSet<string> _chainedEvents = [];
    private readonly HashSet<string> _cinematicEndsPosted = [];
    private readonly HashSet<string> _unenforceableTriggers = [];

    // The objects that enabled triggers own (walls, gates, collision), by trigger name.
    private readonly Dictionary<string, (IActorRef Actor, CoreObject Object, string Tag)> _triggerObjects = new(StringComparer.OrdinalIgnoreCase);

    // The activate event a trigger lists to start the zone with it enabled.
    private const string START_ZONE_EVENT = "StartZone";

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // Triggers are stored in the client data, except for zone transfers.
        // Our QA team has manually recreated this trigger data, and is available within the database.
        // Words cannot describe how thankful I am for QA. They are the unsung heroes of the development team.
        var replacedTriggers = ReplaceTriggerDataWithDatabase(message.TriggerData);
        var spawners = message.SpawnData.m_spawners;
        UpdateSpawnResultTriggers(ref replacedTriggers, spawners, message.PathData.m_pathList, message.NodeData.m_nodeList);

        _chainedEvents.Clear();
        foreach (var chained in replacedTriggers
                     .Where(t => t?.m_results?.m_results is not null)
                     .SelectMany(t => t.m_results.m_results)
                     .OfType<ResPostEvent>()) {
            _chainedEvents.Add(chained.m_eventName.ToString());
        }

        SeedTriggerStates(replacedTriggers);

        _orderedTriggers.Clear();
        foreach (var trigger in replacedTriggers) {
            var triggerActor = Context.ActorOf(Props.Create(() => new ZoneTrigger(ZoneRef, Zone, trigger)));
            BeginEntityLoad(triggerActor, trigger?.m_triggerName);
            _orderedTriggers.Add((trigger, triggerActor));
        }

        // Triggers that start enabled bring their own objects with them.
        _triggerObjects.Clear();
        foreach (var (trigger, _) in _orderedTriggers) {
            if (trigger is not null && Zone.ScriptState.IsTriggerEnabled(trigger.m_triggerName)) {
                SpawnTriggerObject(trigger);
            }
        }

        ReportLoadedWhenEntitiesLoad();
    }

    protected override void OnEntityLoadFailed(IActorRef entityActor) {
        _orderedTriggers.RemoveAll(x => x.Actor.Equals(entityActor));

        foreach (var name in _triggerObjects.Where(x => x.Value.Actor.Equals(entityActor)).Select(x => x.Key).ToList()) {
            _triggerObjects.Remove(name);
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_POSTEVENT))]
    private void ReceivePostEvent(ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // Client wads pair multiple triggers on the same event; every passing trigger fires
        // (their results are independent), but paired teleporter triggers must not both win:
        // only one ResTeleport trigger may execute (see PickTeleportTrigger).
        var passing = new List<(Trigger Trigger, IActorRef Actor)>();
        foreach (var (trigger, triggerActor) in _orderedTriggers) {
            if (trigger.m_fireEvents is null || !trigger.m_fireEvents.Any(x => x == message.EventName)) {
                continue;
            }

            if (!IsLongRaisedEvent(message.EventName) && !IsEnforcedEnabled(trigger)) {
                continue;
            }

            if (ZoneTrigger.HasUndecodableRequirement(trigger.m_requirements) && !IsLongRaisedEvent(message.EventName)) {
                if (_undecodableLogged.Add(trigger.m_triggerName)) {
                    Logger.Warning("Trigger {0} in {1} has a requirement class Imcodec cannot decode; it will not fire.",
                        Logger.Args(trigger.m_triggerName, Zone.ZonePath));
                }

                continue;
            }

            // A trigger with a fire limit (m_triggerMax) that used them all in this zone instance stays quiet.
            if (!Zone.ScriptState.HasTriggerFiresLeft(trigger.m_triggerName, trigger.m_triggerMax)) {
                continue;
            }

            if (!EvaluateRequirements(trigger, message)) {
                continue;
            }

            passing.Add((trigger, triggerActor));
        }

        var teleportWinner = PickTeleportTrigger(passing, message);
        foreach (var (trigger, triggerActor) in passing) {
            // Two passing events in one message cannot both take the last fire.
            if (!Zone.ScriptState.HasTriggerFiresLeft(trigger.m_triggerName, trigger.m_triggerMax)) {
                continue;
            }

            Zone.ScriptState.RecordTriggerFire(trigger.m_triggerName);
            var hasTeleportResult = HasTeleportResult(trigger);
            triggerActor.Forward(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = message.EventName,
                PlayerActor = message.PlayerActor,
                PlayerGameObject = message.PlayerGameObject,
                SuppressTeleportResults = hasTeleportResult && !ReferenceEquals(trigger, teleportWinner),
                PlayerSpawned = message.PlayerSpawned,
                Adjectives = message.Adjectives,
                RequirementsChecked = true,
            });
        }

        // Triggers that fired saw the state from before this event; now the event's own enables and disables land.
        ApplyTriggerStateEvents(message.EventName, message.PlayerActor, message.PlayerGameObject);
    }

    private void SeedTriggerStates(List<Trigger> triggers) {
        // A trigger whose activate events nothing in the zone posts stays enabled, since nothing could ever enable it.
        _unenforceableTriggers.Clear();
        foreach (var trigger in triggers.Where(t => t is not null)) {
            var activate = trigger.m_activateEvents?.Select(e => e.ToString()).ToList() ?? [];
            var startsEnabled = activate.Count == 0 || activate.Contains(START_ZONE_EVENT);
            string name = trigger.m_triggerName;
            Zone.ScriptState.SetTriggerEnabled(name, startsEnabled);

            if (!startsEnabled && !activate.Any(_chainedEvents.Contains)) {
                _unenforceableTriggers.Add(name);
            }
        }
    }

    private bool IsEnforcedEnabled(Trigger trigger) {
        // Only events raised since object, kill and interaction events existed are enforced; older ones fire as they always did.
        string name = trigger.m_triggerName;

        return _unenforceableTriggers.Contains(name) || Zone.ScriptState.IsTriggerEnabled(name);
    }

    private void ApplyTriggerStateEvents(string eventName, IActorRef playerActor, CoreObject playerObject) {
        foreach (var (trigger, _) in _orderedTriggers) {
            string name = trigger?.m_triggerName;
            if (string.IsNullOrEmpty(name)) {
                continue;
            }

            if (trigger.m_activateEvents?.Any(e => e.ToString() == eventName) == true) {
                Zone.ScriptState.SetTriggerEnabled(name, true);
                SpawnTriggerObject(trigger);
            }

            if (trigger.m_deactivateEvents?.Any(e => e.ToString() == eventName) == true) {
                Zone.ScriptState.SetTriggerEnabled(name, false);

                // A trigger's own object (a collision volume, a door) goes away when one of the trigger's
                // deactivate events is posted.
                var objectTag = trigger.m_triggerObjInfo?.m_zoneTag;
                if (!string.IsNullOrEmpty(objectTag)) {
                    ApplyObjectState(objectTag, DEACTIVATED_OBJECT_STATE, playerActor, playerObject);
                }

                DespawnTriggerObject(trigger);
            }
        }
    }

    private void SpawnTriggerObject(Trigger trigger) {
        // The object is a DYNAMIC_SERVER one, so the client only has it once MSG_NEWOBJECT arrives.
        var info = trigger.m_triggerObjInfo;
        string name = trigger.m_triggerName;
        if (info is null) {
            return;
        }

        if (info.m_templateID == 0) {
            return;
        }

        if (string.IsNullOrEmpty(name)) {
            Logger.Debug("Trigger in {0}: object {1} skipped, the trigger has no name.",
                Logger.Args(Zone.ZonePath, info.m_zoneTag));

            return;
        }

        if (_triggerObjects.ContainsKey(name)) {
            Logger.Debug("Trigger {0} in {1}: object {2} skipped, the trigger already has its object.",
                Logger.Args(name, Zone.ZonePath, info.m_zoneTag));

            return;
        }

        var template = CoreObjectFactory.GetCoreTemplate(info.m_templateID);
        if (template is null) {
            Logger.Warning("Trigger {0} in {1} owns object {2} with template ID {3}, which was not found; it is not spawned.",
                Logger.Args(name, Zone.ZonePath, info.m_zoneTag, info.m_templateID));

            return;
        }

        var coreObject = CoreObjectFactory.FinalizeCoreObject(info, template);
        if (coreObject is null) {
            Logger.Debug("Trigger {0} in {1}: object {2} skipped, the core object could not be finalized.",
                Logger.Args(name, Zone.ZonePath, info.m_zoneTag));

            return;
        }

        // TriggerObjectInfo hides the template id with its own property, so the base one the factory copies is 0.
        coreObject.m_templateID = info.m_templateID;

        // The trigger's own object comes back in its start state, not the "Off" it was hidden with.
        var objectTag = info.m_zoneTag;
        if (Zone.ScriptState.IsObjectIn(objectTag, DEACTIVATED_OBJECT_STATE) && !string.IsNullOrEmpty(info.m_startState)) {
            Zone.ScriptState.SetObjectState(objectTag, info.m_startState);
        }

        var actor = CreateEntityActor(coreObject, template, info);
        _triggerObjects[name] = (actor, coreObject, objectTag);
        Logger.Debug("Trigger {0} in {1}: object {2} created, template {3} ({4}), location {5}, start state {6}, drawn by the client: {7}.",
            Logger.Args(name, Zone.ZonePath, objectTag, info.m_templateID, template.GetType().Name, info.m_location,
                info.m_startState, RenderComponent.ShouldAttachToEntity(template)));
    }

    private void DespawnTriggerObject(Trigger trigger) {
        // The object comes back when the trigger is enabled again.
        string name = trigger.m_triggerName;
        if (string.IsNullOrEmpty(name) || !_triggerObjects.Remove(name, out var owned)) {
            return;
        }

        StopTriggerObject(owned.Actor, owned.Object);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVETRIGGEROBJECT))]
    private void ReceiveRemoveTriggerObject(ZONE_102_PROTOCOL.MSG_REMOVETRIGGEROBJECT message) {
        var match = _triggerObjects.FirstOrDefault(x => string.Equals(x.Value.Tag, message.ObjectName, StringComparison.OrdinalIgnoreCase));
        if (match.Key is null || !_triggerObjects.Remove(match.Key, out var owned)) {
            Logger.Debug("Zone {0}: no trigger object {1} to remove.", Logger.Args(Zone.ZonePath, message.ObjectName));

            return;
        }

        StopTriggerObject(owned.Actor, owned.Object);
    }

    private void StopTriggerObject(IActorRef actor, CoreObject triggerObject) {
        ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = triggerObject.m_globalID },
            Targets = ZoneBroadcastTarget.Players,
        });
        ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_RELEASEMOBILEID { MobileId = triggerObject.m_nMobileID });

        EntityActors.Remove(actor);
        Context.Stop(actor);
    }

    private static bool HasTeleportResult(Trigger trigger)
        => trigger.m_results?.m_results?.Any(result => result is ResTeleport) == true;

    private static Trigger PickTeleportTrigger(List<(Trigger Trigger, IActorRef Actor)> passing, ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        // ReqHasQuest also passes for a completed quest: prefer the trigger naming a currently active quest, else the last passing one.
        var teleports = passing.Where(x => HasTeleportResult(x.Trigger)).Select(x => x.Trigger).ToList();
        if (teleports.Count <= 1) {
            return teleports.FirstOrDefault();
        }

        HashSet<string> activeQuests = [];
        if (message.PlayerActor is not null) {
            var wizardResponse = message.PlayerActor
                .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD()).Result;
            activeQuests = wizardResponse.Wizard.QuestBehavior.CurrentQuestInstances
                .Select(q => q.QuestName).ToHashSet();
        }

        var qualifying = teleports
            .Where(t => CollectRequiredQuests(t.m_requirements).Any(activeQuests.Contains))
            .ToList();

        return qualifying.Count == 1 ? qualifying[0] : teleports[^1];
    }

    private static IEnumerable<string> CollectRequiredQuests(RequirementList list) {
        if (list?.m_requirements is null) {
            yield break;
        }

        foreach (var requirement in list.m_requirements) {
            if (requirement is RequirementList nested) {
                foreach (var name in CollectRequiredQuests(nested)) {
                    yield return name;
                }
            }
            else if (requirement is ReqHasQuest hasQuest && !hasQuest.m_applyNOT && !string.IsNullOrEmpty(hasQuest.m_questName)) {
                yield return hasQuest.m_questName;
            }
        }
    }

    // Events raised before object and kill events existed: zone entry, volumes and chained events.
    private bool IsLongRaisedEvent(string eventName)
        => eventName == "EnterZone"
        || eventName.StartsWith("Enter_", StringComparison.Ordinal)
        || eventName.StartsWith("Exit_", StringComparison.Ordinal)
        || _chainedEvents.Contains(eventName);

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_STARTSTAGEDCINEMATIC))]
    private void ReceiveStartStagedCinematic(ZONE_102_PROTOCOL.MSG_STARTSTAGEDCINEMATIC message) {
        // The end event is the waiting trigger's fire event that nothing in the zone posts; posted once per instance, as if the cutscene had played.
        var posted = _orderedTriggers
            .Where(x => x.Trigger?.m_results?.m_results is not null)
            .SelectMany(x => x.Trigger.m_results.m_results)
            .OfType<ResPostEvent>()
            .Select(x => x.m_eventName.ToString())
            .ToHashSet();

        var endEvents = _orderedTriggers
            .Where(x => x.Trigger?.m_fireEvents is not null)
            .SelectMany(x => x.Trigger.m_fireEvents)
            .Select(x => x.ToString())
            .Where(x => x.Contains("Cinematic", StringComparison.OrdinalIgnoreCase)
                && x.Contains("End", StringComparison.OrdinalIgnoreCase)
                && !x.EndsWith(".EnterState", StringComparison.Ordinal)
                && !x.StartsWith("Enter_", StringComparison.Ordinal)
                && !x.StartsWith("Exit_", StringComparison.Ordinal)
                && !posted.Contains(x))
            .Distinct()
            .Where(_cinematicEndsPosted.Add)
            .ToList();

        foreach (var endEvent in endEvents) {
            Self.Tell(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
                EventName = endEvent,
                PlayerActor = message.PlayerActor,
                PlayerGameObject = message.PlayerGameObject,
            });
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_MODIFYTRIGGEROBJECT))]
    private void ReceiveModifyTriggerObject(ZONE_102_PROTOCOL.MSG_MODIFYTRIGGEROBJECT message)
        => ApplyObjectState(message.ObjectName, message.StateName, message.PlayerActor, message.PlayerGameObject, message.PlayerSpawned);

    private void ApplyObjectState(string objectName, string stateName, IActorRef playerActor, CoreObject playerObject, bool playerSpawned = false) {
        if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(stateName)) {
            return;
        }

        Zone.ScriptState.SetObjectState(objectName, stateName);

        ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [new ZONE_102_PROTOCOL.MSG_ENTERSTATE {
                ObjectName = objectName,
                StateName = stateName,
                ExclusiveToSender = false,
            }],
            // Zone objects and the trigger-owned (server-spawned) ones live under different supervisors.
            Targets = ZoneBroadcastTarget.Objects | ZoneBroadcastTarget.Triggers,
        });
        // Client-only objects take the state through a dynamic mod.
        ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Messages = [new CHARACTER_103_PROTOCOL.MSG_SENDDYNAMODSTATE {
                ObjectName = objectName,
                StateName = stateName,
            }],
            Targets = ZoneBroadcastTarget.Players,
        });

        Self.Tell(new ZONE_102_PROTOCOL.MSG_POSTEVENT {
            EventName = $"{objectName}.{stateName}.EnterState",
            PlayerActor = playerActor,
            PlayerGameObject = playerObject,
            PlayerSpawned = playerSpawned,
        });
    }

    private bool EvaluateRequirements(Trigger trigger, ZONE_102_PROTOCOL.MSG_POSTEVENT message) {
        if (trigger.m_requirements?.m_requirements is null || trigger.m_requirements.m_requirements.Count == 0) {
            return true;
        }

        var queryWizardMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var wizardResponse = message.PlayerActor.Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryWizardMsg).Result;

        return RequirementDispatcher.EvaluateRequirements(
            requirements: trigger.m_requirements,
            context: new ZoneRequirementContext(
                trigger.m_requirements,
                message.PlayerActor,
                message.PlayerGameObject,
                wizardResponse.Wizard,
                ZoneRef,
                trigger.m_triggerName) {
                ScriptState = Zone.ScriptState,
                EventAdjectives = message.Adjectives,
            });
    }

    private void UpdateSpawnResultTriggers(ref List<Trigger> clientTriggers, List<SpawnObject> spawners, List<PathObjectTemplate> paths, List<NodeObject> nodes) {
        foreach (var trigger in clientTriggers) {
            if (trigger.m_results == null || trigger.m_results.m_results == null) {
                continue;
            }

            foreach (var result in trigger.m_results.m_results) {
                if (result is ResSpawn resSpawn) {
                    var spawnObjectList = spawners.Find(x => x.m_id == resSpawn.m_spawnID);
                    if (spawnObjectList != null) {
                        var spawnObject = spawnObjectList.m_spawnList[0];
                        var objectPath = paths.Find(x => x.m_id.Full == spawnObject.m_objectInfo.m_pathID.Full);
                        resSpawn.nodes = nodes.FindAll(x => objectPath.m_nodeIDs.Contains(x.m_id));
                        resSpawn.templateID = spawnObject.m_objectInfo.m_templateID;
                    }
                }
                else if (result is ResDespawn resDespawn) {
                    // The wad data only names the spawner; resolve the template so the removal can match an entity.
                    var spawnObjectList = spawners.Find(x => x.m_id == resDespawn.m_spawnID);
                    if (spawnObjectList != null) {
                        resDespawn.m_templateID = spawnObjectList.m_spawnList[0].m_objectInfo.m_templateID;
                    }
                }
            }
        }
    }

    private List<Trigger> ReplaceTriggerDataWithDatabase(WizZoneTriggers clientTriggers) {
        var triggers = new List<Trigger>(clientTriggers.m_triggers);
        var zoneName = Zone.ZonePath;

        // One single database query: great!
        var databaseTriggers = ZoneDataCollection.GetZoneData(zoneName);

        foreach (var trigger in triggers) {
            if (trigger is null) {
                continue;
            }

            // If there's persistent data associated with this trigger, load it.
            var persistentTriggerData = databaseTriggers?.Teleports
                .FirstOrDefault(x => x.TriggerName == trigger.m_triggerName);

            if (persistentTriggerData is not null) {
                // April Fools: if we've confirmed this is a zone transfer, we'll instead
                // grab a random zone transfer from the database. Then, we'll set the trigger
                // results to the random zone transfer.
                if (s_randomizeGateways) {
                    var randomZoneData = ZoneDataCollection.GetAprilFoolsRandomZoneData();
                    var randomIdx = new Random().Next(randomZoneData.Teleports.Count - 1);
                    var randomZoneTransfer = randomZoneData.Teleports[randomIdx];

                    persistentTriggerData.Teleport = randomZoneTransfer.Teleport;
                }

                // A trigger that already teleports (a ride waits, then teleports) keeps its other results and takes
                // the stored destination; any other trigger's results become the stored teleport.
                var existing = trigger.m_results?.m_results;
                var resultList = new ResultList {
                    m_results = existing is not null && existing.Any(r => r is ResTeleport)
                        ? [.. existing.Select(r => r is ResTeleport ? persistentTriggerData.Teleport : r)]
                        : [persistentTriggerData.Teleport]
                };
                trigger.m_results = resultList;
            }
        }

        return triggers;
    }

}
