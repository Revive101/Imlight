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

using Akka.Actor;
using Imcodec.Math;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor 
/// for any volumes that are created within the zone.
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
internal sealed class ZoneVolumeSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        foreach (var volume in message.VolumeData.m_volumes) {
            // We have to use this explicit method because the volume has two `m_templateID` fields, but only the duplicate one is used.
            var coreObject = CoreObjectFactory.FinalizeCoreObject(volume, volume.m_templateID);
            if (coreObject is null) {
                continue;
            }

            // Set data for this CoreObject from the given volume data.
            var loc = new Vector3(volume.m_locationX, volume.m_locationY, volume.m_locationZ);
            coreObject.m_location = loc;
            // For some reason, the volume type has two `m_templateID` fields, but only the duplicate one is used.
            coreObject.m_templateID = volume.m_templateID; // I've never seen this templateID be anything but 1700.
            coreObject.m_debugName = volume.m_volumeName;

            var template = CoreObjectFactory.GetCoreTemplate(volume.m_templateID);
            var objectActor = CreateEntityActor(coreObject, template, null);

            EntityActors.Add(objectActor);

            // Send the volume details to the object actor.
            var volumeDetails = new ZONE_102_PROTOCOL.MSG_VOLUMEDETAILS { 
                Volume = volume,
            };
            objectActor.Tell(volumeDetails);
        }

        // Inform the zone that we have finished initializing all objects.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = nameof(ZoneVolumeSupervisor) };
        Sender.Tell(reply);
    }

}