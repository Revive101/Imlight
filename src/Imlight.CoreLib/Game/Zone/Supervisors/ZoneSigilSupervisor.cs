/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Math;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

internal sealed class ZoneSigilSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    private readonly Dictionary<CoreObject, IActorRef> _sigils = [];
    
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
            var coreObject = CoreObjectFactory.FinalizeCoreObject(objectInfo, template);

            // Sometimes the sigil may spawn in the ground.
            var newLoc = coreObject.m_location;
            newLoc.Y += 0.5f;
            coreObject.m_location = newLoc;

            // Create the sigil actor and inform it of the details.
            var objectActor = CreateEntityActor(coreObject, template);
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