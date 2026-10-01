using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

/// <summary>
/// ResAddTriggerObject: a trigger brings back a zone object by its zone tag, one a ResRemoveTriggerObject hid, in the
/// state the result names. The zone shows it to every player.
/// </summary>
internal sealed class ResAddTriggerObjectHandler : BaseResultHandler<ResAddTriggerObject> {

    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor is null || Result is null) {
            return false;
        }

        zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_ADDTRIGGEROBJECT {
            ObjectName = Result.m_triggerObjName.ToString(),
            StateName = Result.m_triggerObjState.ToString(),
        }, Akka.Actor.ActorRefs.NoSender);

        return true;
    }

}
