using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Net;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;

namespace Imlight.Login.Services
{
    internal class GameTransitionService : MessageService
    {
        public GameTransitionService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new GameTransitionService(parentActor));
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_SELECTCHARACTER))]
        private void ReceiveSelectCharacter(LOGIN_7_PROTOCOL.MSG_SELECTCHARACTER message)
        {
            var charSelectedMsg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED()
            {
                IP = "127.0.0.1", // @FIXME: This should be sourced from elsewhere.
                TCPPort = 12600,  // @FIXME: This should be sourced from elsewhere.
                UDPPort = 12600,  // @FIXME: This should be sourced from elsewhere.
                Key = new ByteString(),
                UserID = 0,
                CharID = 0,
                ZoneID = new GID(123004564835992122),
                ZoneName = "WizardCity/WC_Ravenwood",
                Location = "Start",
                Slot = 0,
                PrepPhase = 0,
                Error = 0,
                LoginServer = "Imlight.Login" // @FIXME: This should be sourced from elsewhere.
            };

            SendToSocket(charSelectedMsg);
        }
    }
}
