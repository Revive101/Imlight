using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Game.Results.Handlers;

/// <summary>
/// ResPostQuestEvent: a quest event for the acting player. Goals that list it in their generic events complete.
/// </summary>
internal sealed class ResPostQuestEventHandler : BaseResultHandler<ResPostQuestEvent> {

    public override bool Execute(IResultContext context) {
        var playerRef = context.GetPlayerRef();
        if (playerRef is null || Result is null) {
            return false;
        }

        playerRef.Tell(new ZONE_102_PROTOCOL.MSG_ZONEEVENTFORQUESTS {
            EventName = Result.m_eventName.ToString(),
        }, Akka.Actor.ActorRefs.NoSender);

        return true;
    }

}
