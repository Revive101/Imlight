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
using SharpDX;

namespace Imlight.CoreLib.Game.WizardZone.Supervisors;

/// <summary>
/// Exists as a child actor of a <see cref="Zone"/> and is the supervisor 
/// for any volumes that are created within the zone.
/// </summary>
/// <param name="wizardZoneRef">The reference to the parent <see cref="WizardZone"/>.</param>
/// <param name="zone">The zone that this supervisor is responsible for.</param>
internal sealed class ZoneVolumeSupervisor(IActorRef wizardZoneRef, Core.Zone zone) : ZoneEntitySupervisor(wizardZoneRef, zone) {

    private ushort _reservedIdCounter = Core.Zone.RESERVED_VOLUME_ID_MIN;

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
            coreObject.m_nMobileID = GetReservedMobileID();
            coreObject.m_debugName = volume.m_volumeName;

            var objectActor = CreateEntityActor(coreObject, null);

            EntityActors.Add(objectActor);
        }
    }

    private ushort GetReservedMobileID() {
        var max = Core.Zone.RESERVED_VOLUME_ID_MAX;

        if (_reservedIdCounter + 1 >= max) {
            Logger.Fatal("ZoneObjectSupervisor has run out of reserved mobile IDs. " +
                         "Minimum reserved ID: {MinReservedID}, Maximum reserved ID: {MaxReservedID}",
                         Logger.Args(Core.Zone.RESERVED_VOLUME_ID_MAX, max));
        }

        return _reservedIdCounter++;
    }

}