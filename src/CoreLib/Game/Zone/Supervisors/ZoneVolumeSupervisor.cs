/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using SharpDX;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor 
/// for any volumes that are created within the zone.
/// </summary>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
internal sealed class ZoneVolumeSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    private void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
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
            var objectActor = CreateEntityActor(coreObject, template);

            EntityActors.Add(objectActor);

            // Send the volume details to the object actor.
            var volumeDetails = new ZONE_102_PROTOCOL.MSG_ADDVOLUME { 
                Volume = volume,
            };
            objectActor.Tell(volumeDetails);
        }

        // Inform the zone that we have finished initializing all objects.
        var reply = new ZONE_102_PROTOCOL.MSG_ZONESUPERVISORLOADRESULTS { SupervisorName = nameof(ZoneVolumeSupervisor) };
        Sender.Tell(reply);
    }

}