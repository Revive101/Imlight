using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

/// <summary>
/// ResRemoveTriggerObject: a trigger removes an object by its zone tag, one another trigger owns (a wall, a gate)
/// or one placed in the zone (a puzzle object). The zone removes it for every player.
/// </summary>
internal sealed class ResRemoveTriggerObjectHandler : BaseResultHandler<ResRemoveTriggerObject> {

    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor is null || Result is null) {
            return false;
        }

        zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_REMOVETRIGGEROBJECT {
            ObjectName = Result.m_triggerObjName.ToString(),
        }, Akka.Actor.ActorRefs.NoSender);

        return true;
    }

}
