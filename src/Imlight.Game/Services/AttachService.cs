using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net;
using Imlight.Data;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;

namespace Imlight.Game.Services
{
    internal class AttachService : MessageService
    {
        public AttachService(SessionActor sessionActor) : base(sessionActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new AttachService(parentActor));
        }

        [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_ATTACH))]
        private void ReceiveAttach(GAME_5_PROTOCOL.MSG_ATTACH message)
        {
            var data = GetCharacterData(message.CharID);

            var loginCompleteMsg = new GAME_5_PROTOCOL.MSG_LOGINCOMPLETE()
            {
                Data = GetCharacterData(0),
                ZoneName = "WizardCity/WC_Ravenwood",
                DynamicZoneID = 4288020480,
                DynamicServerProcID = 57781,
                IsCSR = 1,
                Permissions = 31679,
                RealmName = "Imlight",
                ZoneID = new GID(4288020480),
            };

            SendToSocket(loginCompleteMsg);
        }

        private ByteString GetCharacterData(ulong charId)
        {
            // =============================================================
            // THIS IS ENTIRELY DEBUG ONLY AND MUST BE REMOVED LATER
            // =============================================================
            var character = Data.Util.GetDebugAccount();

            return new ByteString();
        }
    }
}
