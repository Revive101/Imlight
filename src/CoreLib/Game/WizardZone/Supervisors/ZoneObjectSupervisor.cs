/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.WizardZone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.WizardZone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor 
/// for any objects that are created within the zone.
/// <remarks>Initializes any <see cref="CoreObjectInfo"/> found withing <see cref="ZoneData.m_objectList"/> field of the
/// given zone data.</remarks>
/// </summary>
/// <param name="wizardZoneRef"></param>
internal sealed class ZoneObjectSupervisor(IActorRef wizardZoneRef, Core.Zone zone) : ReceiveProtocolDispatcher {

    private readonly IActorRef _wizardZoneRef = wizardZoneRef;
    private readonly Core.Zone _zone = zone;
    private readonly List<IActorRef> _objectActors = [];
    private ushort _reservedIdCounter = Core.Zone.RESERVED_OBJECT_ID_MIN;

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    private void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // We only care about the ZoneData section of the message.
        var zoneData = message.ZoneData;

        // Initialize any objects found within the zone data.
        foreach (var objectInfo in zoneData.m_objectList) {
            // Some objects may be flagged as holiday objects, which means they should only be
            // spawned during certain times of the year.
            if (!IsObjectEligibleForSpawn(objectInfo)) {
                continue;
            }

            var template = (GameObjectTemplate) CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            var coreObject = InitializeObject(objectInfo, template);

            // Create a new object actor for the core object. Expect a reply from the object actor
            // to indicate that object has finished initializing.
            var objectActor = Context.ActorOf(Props.Create(() => new ZoneEntity(coreObject, template, _wizardZoneRef, _zone)));
            // todo: await reply

            _objectActors.Add(objectActor);
        }

        // Inform the zone that we have finished initializing all objects.
        Sender.Tell(message);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST))]
    private void ReceiveZoneBroadcast(ZONE_102_PROTOCOL.MSG_ZONEOBJECTBROADCAST message) {
        foreach (var actor in _objectActors) {
            foreach (var internlMessage in message.Messages) {
                actor.Tell(internlMessage);
            }
        }
    }

    private CoreObject InitializeObject(CoreObjectInfo objectInfo, GameObjectTemplate template) {
        var newObject = CoreObjectFactory.FinalizeCoreObject(objectInfo, template);
        newObject.m_nMobileID = GetReservedMobileID();

        return newObject;
    }

    private ushort GetReservedMobileID() {
        var max = Core.Zone.RESERVED_OBJECT_ID_MAX;

        if (_reservedIdCounter + 1 >= max) {
            Logger.Fatal("ZoneObjectSupervisor has run out of reserved mobile IDs. " +
                         "Minimum reserved ID: {MinReservedID}, Maximum reserved ID: {MaxReservedID}",
                         Logger.Args(Core.Zone.RESERVED_OBJECT_ID_MIN, max));
        }

        return _reservedIdCounter++;
    }

    private static bool IsObjectEligibleForSpawn(CoreObjectInfo objectInfo) {
        if (objectInfo is null) {
            return false;
        }

        if (objectInfo.m_spawnRequirements is not null) {
            var requirements = objectInfo.m_spawnRequirements.m_requirements.ToList();
            var operatorType = objectInfo.m_spawnRequirements.m_operator;
            
            return CheckGlobalRegistryRequirements(requirements, operatorType);
        }

        return true;
    }

    private static bool CheckGlobalRegistryRequirements(List<Requirement> values, Requirement.Operator operatorType) {
        var allMatched = true;

        foreach (var requirement in values) {
            if (requirement is ReqGlobalRegistryValue globalReq) {
                if (!GlobalRegistryValueMet(globalReq)
                    && operatorType == Requirement.Operator.ROP_AND) {
                    return false;
                }

                allMatched = allMatched && !globalReq.m_applyNOT;
            }
            else {
                Logger.Warning("Holy!!! We found a spawn requirement that isn't a global registry value. " +
                            "This is a problem. Let Jooty know.");
            }
        }

        return allMatched;
    }

    private static bool GlobalRegistryValueMet(ReqGlobalRegistryValue value) {
        var globalValue = GlobalRegistryCollection.GetRegistryEntry(value.m_entryName);

        switch (value.m_operatorType) {
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_EQUALS:
                return value.m_numericValue == globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_LESS_THAN:
                return value.m_numericValue < globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_LESS_THAN_EQ:
                return value.m_numericValue <= globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_GREATER_THAN:
                return value.m_numericValue > globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_GREATER_THAN_EQ:
                return value.m_numericValue >= globalValue;
            case ReqNumeric.OPERATOR_TYPE.OPERATOR_UNKNOWN:
            default: {
                    Logger.Error("Zone contains a spawn requirement that " +
                                      "references a global registry value that does not exist. " +
                                      "Entry name: {EntryName}", Logger.Args(value.m_entryName));
                    return false;
                }
        }
    }

}