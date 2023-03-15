using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Crypto;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler.DML;
using WizUnraveler.Cache;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net.Services;

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
            // For now, we'll always except any user.
            SendInternal(new ACCOUNT_104_PROTOCOL.INTMSG_SET_ACCOUNT()
            {
                Account = Data.Util.GetDebugAccount()
            });
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
