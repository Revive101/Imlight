using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

/// <summary>
/// ResModifyTriggerObject: a trigger puts a named zone object (a door, an obelisk, a brazier) into a state.
/// The zone applies it for everyone and raises the object's EnterState event.
/// </summary>
internal sealed class ResModifyTriggerObjectHandler : BaseResultHandler<ResModifyTriggerObject> {

    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor is null || Result is null) {
            return false;
        }

        zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_MODIFYTRIGGEROBJECT {
            ObjectName = Result.m_triggerObjName.ToString(),
            StateName = Result.m_triggerObjState.ToString(),
            PlayerActor = context.GetPlayerRef(),
            PlayerGameObject = context.GetPlayerObj(),
            PlayerSpawned = context.IsPlayerSpawn()
        }, Akka.Actor.ActorRefs.NoSender);

        return true;
    }

}
