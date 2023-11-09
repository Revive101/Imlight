using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Shared.Networking;

namespace Imlight.CoreLib.Game.Services;

public class FriendsService : MessageService {
    public FriendsService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new FriendsService(parentActor));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYREQUESTLIST))]
    private void ReceiveBuddyRequestList(GAME_5_PROTOCOL.MSG_BUDDYREQUESTLIST message) {
        Logger.Debug("ListOwnerGID: " + message.ListOwnerGID + ", Forwarded: " + message.Forwarded);
    }
}
