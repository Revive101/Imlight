/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor 
/// for any objects that are created within the zone.
/// <remarks>Initializes any <see cref="CoreObjectInfo"/> found within <see cref="ZoneData.m_objectList"/> field of the
/// given zone data.</remarks>
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
internal sealed class ZoneObjectSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    private void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // We only care about the ZoneData section of the message.
        var zoneData = message.ZoneData;

        // Initialize any objects found within the zone data.
        foreach (var objectInfo in zoneData.m_objectList) {
            // Some objects may be flagged as holiday objects, which means they should only be
            // spawned during certain times of the year.5
            if (!IsObjectEligibleForSpawn(objectInfo)) {
                continue;
            }

            var template = (GameObjectTemplate) CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            var coreObject = CoreObjectFactory.FinalizeCoreObject(objectInfo, template);;
            var objectActor = CreateEntityActor(coreObject, template);
        }

        // Inform the zone that we have finished initializing all objects.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = nameof(ZoneObjectSupervisor) };
        Sender.Tell(reply);
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