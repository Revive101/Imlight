using Akka.Actor;
using Imlight.Common.Serializable.Caches;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;

namespace Imlight.Server.Game.Services;

public class FriendsService : MessageService
{
    public FriendsService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
    {
        return Akka.Actor.Props.Create(() => new FriendsService(parentActor));
    }

    [MessageHandler(typeof(GAME.MSG_BUDDYREQUESTLIST))]
    private void ReceiveBuddyRequestList(GAME.MSG_BUDDYREQUESTLIST message)
    {
        Log.Debug("ListOwnerGID: " + message.ListOwnerGID + ", Forwarded: " + message.Forwarded);
    }
}