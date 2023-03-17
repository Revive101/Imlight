using Akka.Actor;
using Imlight.Net;
using WizUnraveler.Cache;

namespace Imlight.Game.Services
{
    public class ClientService : MessageService
    {
        public ClientService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new ClientService(parentActor));
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
        private void ReceiveClientDisconnect(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT message)
        {
            SessionActor.Dispose();
        }
        
        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
        private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message)
        {
            SendToSocket(new GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT());
            
            SessionActor.Dispose();
        }
    }
}