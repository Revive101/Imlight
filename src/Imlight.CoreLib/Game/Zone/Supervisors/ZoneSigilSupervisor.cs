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

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Math;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// This class is responsible for managing the sigils within a zone. It handles the
/// loading of sigils, their initialization, and the forwarding of messages to them.
/// </summary>
internal sealed class ZoneSigilSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    private readonly Dictionary<CoreObject, IActorRef> _sigils = [];
    
    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        // We only care about the ZoneData section of the message.
        var zoneData = message.ZoneData;

        foreach (var objectInfo in zoneData.m_objectList) {
            if (objectInfo is not MinigameSigilInfo) {
                continue;
            }

            var template = CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID) as GameObjectTemplate;
            if (template is null) {
                Logger.Warning("Dungeon sigil template {0} not found; skipping.",
                    Logger.Args(objectInfo.m_templateID));

                continue;
            }

            var coreObject = CoreObjectFactory.FinalizeCoreObject(objectInfo, template);
            if (coreObject is null) {
                Logger.Warning("Could not finalize dungeon sigil (template {0}); skipping.",
                    Logger.Args(objectInfo.m_templateID));

                continue;
            }

            // The pad octagon sits low and clips into the terrain; lift it like the combat sigils.
            var newLoc = coreObject.m_location;
            newLoc.Z += 5;
            coreObject.m_location = newLoc;

            CreateEntityActor(coreObject, template, objectInfo);
        }

        // Initialize any objects found within the zone data.
        foreach (var objectInfo in zoneData.m_objectList) {
            if (!IsObjectEligibleForSpawn(objectInfo)) {
                continue;
            }

            var template = (GameObjectTemplate) CoreObjectFactory.GetCoreTemplate(objectInfo.m_templateID);
            var coreObject = CoreObjectFactory.FinalizeCoreObject(objectInfo, template);

            // Sometimes the sigil may spawn in the ground.
            var newLoc = coreObject.m_location;
            newLoc.Z += 5;
            coreObject.m_location = newLoc;

            // Create the sigil actor and inform it of the details.
            var objectActor = CreateEntityActor(coreObject, template, objectInfo);
            var detailsMsg = new ZONE_102_PROTOCOL.MSG_SIGILDETAILS {
                CombatSigilObjectInfo = (CombatSigilObjectInfo) objectInfo,
            };
            objectActor.Tell(detailsMsg);

            _sigils.Add(coreObject, objectActor);
        }

        // Inform the zone that we have finished initializing all objects.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = nameof(ZoneObjectSupervisor) };
        Sender.Tell(reply);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL))]
    private void ReceiveRequestCombatSigil(ZONE_102_PROTOCOL.MSG_REQUESTCOMBATSIGIL message) {
        var firstParticipantObj = message.StartingParticipants.Values.First();

        // Find the sigil that is closest to the first participant. Forward
        // the request to that sigil.
        var closestSigil = _sigils
            .OrderBy(x => Vector3.Distance(x.Key.m_location, firstParticipantObj.m_location))
            .FirstOrDefault();
        
        closestSigil.Value?.Forward(message);
    }

    private static bool IsObjectEligibleForSpawn(CoreObjectInfo objectInfo) 
        => objectInfo is not null and CombatSigilObjectInfo;

}