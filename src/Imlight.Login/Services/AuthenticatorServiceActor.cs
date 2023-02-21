using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler.DML;
using WizUnraveler.Cache;

namespace Imlight.Login.Services
{
    internal class AuthenticatorServiceActor : MessageService
    {
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
             * 
             * In all cases, we return `MSG_USER_VALIDATE_RSP`, which contains a potential error code.
             * The error code is a string hash of the error.
             * ""                <-- No error, or successful.
             * "AccountBanned"
             * "MachineBanned"
             * "ValidateFailed"
             * "Timeout"
             * (There is more flags here, but it's difficult to tell what they mean.)
             */

            // For now, we'll always except any user.
            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP()
            {
                UserID = message.UserID,
                PayingUser = 1,
                Error = (int)UserValidateError.NoError,
                Reason = "", // Unclear as to what this field means, but it's most likely an elaboration of an error.
            }, Context.Self);
        }
    }
}
