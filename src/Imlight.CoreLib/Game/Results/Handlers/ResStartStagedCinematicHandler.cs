using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

/// <summary>
/// ResStartStagedCinematic: cutscenes are not played, so the zone raises the end event the triggers wait for.
/// </summary>
internal sealed class ResStartStagedCinematicHandler : BaseResultHandler<ResStartStagedCinematic> {

    public override bool Execute(IResultContext context) {
        var zoneActor = context.GetZoneActor();
        if (zoneActor is null || Result is null) {
            return false;
        }

        zoneActor.Tell(new ZONE_102_PROTOCOL.MSG_STARTSTAGEDCINEMATIC {
            PlayerActor = context.GetPlayerRef(),
            PlayerGameObject = context.GetPlayerObj(),
        }, Akka.Actor.ActorRefs.NoSender);

        return true;
    }

}
