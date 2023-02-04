using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Crypto;
using Imlight.Net;
using WizUnraveler;
using WizUnraveler.Cache;

namespace Imlight.Login
{
    public class LoginManagerActor : ServerReceiverActor
    {
        private const int CLIENT_KEY_TIMEOUT = 120;

        private TimedList<string> _clientKeys;

        public LoginManagerActor(string Name, sbyte ID, ushort port) : base(Name, ID, port) 
        {
            _clientKeys = new TimedList<string>(CLIENT_KEY_TIMEOUT);
        }

        public static Props Props(string Name, sbyte ID, ushort port)
        {
            return Akka.Actor.Props.Create(() => new LoginManagerActor(Name, ID, port));
        }

        protected override void ConfigureReceivers()
        {
            base.ConfigureReceivers();

            Receive<LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK>(x => ReceiveLoginNotAFK(x));
            Receive<LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3>(x => ReceiveUserAuthenV3(x));
            Receive<LOGIN_7_PROTOCOL.MSG_USER_VALIDATE>(x => ReceiveUserValidate(x));
            Receive<LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST>(x => ReceiveRequestCharacterList(x));
            Receive<LOGIN_7_PROTOCOL.MSG_CREATECHARACTER>(x => ReceiveCreateCharacter(x));
        }

        private void ReceiveLoginNotAFK(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK message)
        {
            // @TODO
        }

        private void ReceiveUserAuthenV3(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3 message)
        {
            // @FIXME: This is under major testing.

            // Get the current session and set some details.
            if (!TryGetSession(Sender, out var session))
            {
                Log.Logger.Error($"ServerManagerActor [{Name}] could not get session for sender [{Sender.Path}].");
                return;
            }

            var sessionID = session.SessionID;
            var epoch = session.SessionStartTime;
            var milli = session.SessionMilliseconds;

            // This part, for now, is just debugging. When Imlight can speak to WizAPI, then we can
            // start *really* doing authentication.
            var record = Rec1.Decode(message.Rec1, sessionID, epoch, milli)
                .ToString()
                .Split(' ');
            var username = record[1];
            var CK1 = record[2];
            var passwordEquals = ClientKey.VerifyCK1("password", sessionID, epoch, milli, CK1);

            // Anything below this is when the user is successfully validated.

            // Build ClientKey2. This is a hashed key that a client's game will send with `MSG_USER_VALIDATE`.
            // We can use CK2 to verify a valid session, and that a user has not bypassed our patch client.
            var CK2 = ClientKey.EncodeCK2(sessionID, epoch, milli);
            _clientKeys.Add(CK2);

            // Build response.
            var recordResponse = Rec1.Encode(sessionID, username, CK2, epoch, milli);
            var rsp = new LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_RSP()
            {
                Error = (int)Crypto.HashString(""),
                UserID = 1000,
                Rec1 = recordResponse,
                Reason = "", // ??
                TimeStamp = "",
                PayingUser = 1,
                Flags = 0
            };
            Context.Sender.Tell(rsp);
        }

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
            });
        }

        private void ReceiveCreateCharacter(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER message)
        {
            var data = message.CreationInfo;

            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_CREATECHARACTERRESPONSE());
            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_STARTCHARACTERLIST());
            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_CHARACTERINFO()
            {
                CharacterInfo = data
            });
            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST());
        }

        private void ReceiveRequestCharacterList(LOGIN_7_PROTOCOL.MSG_REQUESTCHARACTERLIST message)
        {
            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_STARTCHARACTERLIST());
            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST());
        }
    }
}
