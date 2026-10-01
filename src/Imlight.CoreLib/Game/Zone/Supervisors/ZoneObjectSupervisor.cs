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
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor 
/// for any objects that are created within the zone.
/// <remarks>Initializes any <see cref="CoreObjectInfo"/> found within <see cref="ZoneData.m_objectList"/> field of the
/// given zone data.</remarks>
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
internal sealed class ZoneObjectSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    // The zone's placed objects with the data to create one again; the actor is null while a trigger has it removed.
    private readonly List<PlacedObject> _placed = [];

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // We only care about the ZoneData section of the message.
        var zoneData = message.ZoneData;

        // Initialize any objects found within the zone data.
        foreach (var objectInfo in zoneData.m_objectList) {
            if (!IsObjectEligibleForSpawn(objectInfo)) {
                continue;
            }

            var template = (GameObjectTemplate) CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            if (template is null) {
                Logger.Warning("Could not create {0} because template ID {1} was not found.",
                    Logger.Args(objectInfo.m_zoneTag, objectInfo.m_templateID.Full));

                continue;
            }

            var coreObject = CoreObjectFactory.FinalizeCoreObject(objectInfo, template);
            if (coreObject is null) {
                Logger.Warning("Could not finalize CoreObject {0} with template ID {1}.",
                    Logger.Args(objectInfo.m_zoneTag, objectInfo.m_templateID.Full));

                continue;
            }

            // Determine if this is a critical object.
            // If so, register it with the Zone.
            if (IsCriticalObject(template)) {
                RegisterCriticalObject(coreObject.m_globalID);
            }

            var actor = CreateEntityActor(coreObject, template, objectInfo);
            _placed.Add(new PlacedObject(objectInfo, template, coreObject, actor));
        }

        ReportLoadedWhenEntitiesLoad();
    }

    private static bool IsObjectEligibleForSpawn(CoreObjectInfo objectInfo) {
        if (objectInfo is null) {
            return false;
        }

        // Do not spawn combat sigils within this supervisor.
        if (objectInfo is CombatSigilObjectInfo) {
            return false;
        }

        // Dungeon-entry sigil pads spawn within ZoneSigilSupervisor.
        if (objectInfo is MinigameSigilInfo) {
            return false;
        }

        return true;
    }

    private static bool IsCriticalObject(CoreTemplate template) {
        if (template is not GameObjectTemplate goTemplate) {
            return false;
        }

        // Check if the adjectives contain "Critical."
        var adjectives = goTemplate.m_adjectiveList;
        return adjectives is not null
            && adjectives.Any(adj => adj.Equals("Critical", StringComparison.OrdinalIgnoreCase));
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SPAWNENTITY))]
    private void ReceiveSpawnEntity(ZONE_102_PROTOCOL.MSG_SPAWNENTITY message) {
        var coreObject = message.CoreObject;
        var template = message.Template;

        var objectActor = CreateEntityActor(coreObject, template, null);
        if (objectActor is null) {
            Logger.Error("Failed to spawn entity from MSG_SPAWNENTITY.");
            return;
        }

        // Spawned entities (summoned pets) can be stopped while the zone lives on.
        Context.Watch(objectActor);

        // Reply with the created entity.
        var rsp = new ZONE_102_PROTOCOL.MSG_SPAWNENTITYRSP {
            EntityActor = objectActor,
            SpawnedObject = coreObject
        };
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVETRIGGEROBJECT))]
    private void ReceiveRemoveTriggerObject(ZONE_102_PROTOCOL.MSG_REMOVETRIGGEROBJECT message) {
        foreach (var placed in _placed.Where(p => p.Actor is not null && IsNamed(p, message.ObjectName))) {
            ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
                Message = new GAME_5_PROTOCOL.MSG_REMOVEOBJECT { GameObjectID = placed.Object.m_globalID },
                Targets = ZoneBroadcastTarget.Players,
            });
            ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_RELEASEMOBILEID { MobileId = placed.Object.m_nMobileID });

            EntityActors.Remove(placed.Actor);
            Context.Stop(placed.Actor);
            placed.Actor = null;
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ADDTRIGGEROBJECT))]
    private void ReceiveAddTriggerObject(ZONE_102_PROTOCOL.MSG_ADDTRIGGEROBJECT message) {
        var named = _placed.Where(p => IsNamed(p, message.ObjectName)).ToList();
        if (named.Count == 0) {
            return;
        }

        // The object comes back in the state the result names; one that never left takes it as a state change.
        if (!string.IsNullOrEmpty(message.StateName)) {
            Zone.ScriptState.SetObjectState(message.ObjectName, message.StateName);
        }

        foreach (var placed in named) {
            if (placed.Actor is null) {
                placed.Object = CoreObjectFactory.FinalizeCoreObject(placed.Info, placed.Template);
                placed.Actor = CreateEntityActor(placed.Object, placed.Template, placed.Info);
            }
            else if (!string.IsNullOrEmpty(message.StateName)) {
                ZoneRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
                    Messages = [new ZONE_102_PROTOCOL.MSG_ENTERSTATE {
                        ObjectName = message.ObjectName,
                        StateName = message.StateName,
                        ExclusiveToSender = false,
                    }],
                    Targets = ZoneBroadcastTarget.Objects,
                });
            }
        }
    }

    [MessageHandler(typeof(Terminated))]
    private void ReceiveEntityTerminated(Terminated message)
        => EntityActors.Remove(message.ActorRef);

    private static bool IsNamed(PlacedObject placed, string zoneTag)
        => string.Equals(placed.Info.m_zoneTag, zoneTag, StringComparison.OrdinalIgnoreCase);

    private void RegisterCriticalObject(GID id) {
        var msg = new ZONE_102_PROTOCOL.MSG_REGISTERCRITICALOBJECT {
            ObjectID = id
        };

        base.ZoneRef.Tell(msg);
    }

    private sealed class PlacedObject(CoreObjectInfo info, GameObjectTemplate template, CoreObject coreObject, IActorRef actor) {

        public CoreObjectInfo Info { get; } = info;
        public GameObjectTemplate Template { get; } = template;
        public CoreObject Object { get; set; } = coreObject;
        public IActorRef Actor { get; set; } = actor;

    }

}
