using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler.DML;
using WizUnraveler.Cache;
using Akka.Actor;

namespace Imlight.Login.Services
{
    internal class AuthenticatorService : MessageService
    {
        public AuthenticatorService(SessionActor parentActor) : base(parentActor) { }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new AuthenticatorService(parentActor));
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE))]
        private void ReceiveUserValidate(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE message)
        {
            // @TODO: User authentication here !

            /*
             * Writing notes here for later:
             * 
             * `MSG_USER_AUTHEN_RSP` will send back another `Rec1` field. It's encoded exactly as before,
             * except the `CK1` field will instead be replaced by a `CK2` field. This is `ClientKey2`. It's a session
             * key responsible for authenticating a game client once the launcher closes.
             * 
             * When the game client starts, it will send `MSG_USER_VALIDATE`. This message contains a field labeled `PassKey3`.
             * `PassKey3` uses the same hashing algorithm as `ClientKey1`, and the original input will be the `ClientKey2` we
             * gave to the client earlier.
             * 
             * Using the cached information, we can hash and compare this request to see if any valid game sessions exist.
             */

            // For now, we'll always except any user.
            SendInternal(new INTMSG_SETACCOUNT(Data.Util.GetDebugAccount(), 0));
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP()
            {
                UserID = message.UserID,
                PayingUser = 1,
                Error = (int)UserValidateError.NoError,
                Reason = "", // Unclear as to what this field means, but it's most likely an elaboration of an error.
            });
            SendToSocket(new LOGIN_7_PROTOCOL.MSG_USER_ADMIT_IND()
            {
                PositionInQueue = 0,
                Status = 1,
            });
        }
    }
}
