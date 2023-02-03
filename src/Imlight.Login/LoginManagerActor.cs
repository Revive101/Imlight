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

            Receive<CommunicationDMLContext>(x => x.Is(typeof(LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3)), x => ReceiveUserAuthenV3(x));
            Receive<CommunicationDMLContext>(x => x.Is(typeof(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE)), x => ReceiveUserValidate(x));
            Receive<CommunicationDMLContext>(x => x.Is(typeof(LOGIN_7_PROTOCOL.MSG_CREATECHARACTER)), x => ReceiveCreateCharacter(x));
        }

        private void ReceiveUserAuthenV3(CommunicationDMLContext message)
        {
            // @FIXME: This is under major testing.
            var msg = (LOGIN_7_PROTOCOL.MSG_USER_AUTHEN_V3)message.Message;
            var sessionID = message.Actor.SessionID;
            var epoch = message.Actor.SessionStartTime;
            var milli = message.Actor.SessionMilliseconds;

            // This part, for now, is just debugging. When Imlight can speak to WizAPI, then we can
            // start *really* doing authentication.
            var record = Rec1.Decode(msg.Rec1, sessionID, epoch, milli)
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

        private void ReceiveUserValidate(CommunicationDMLContext message)
        {
            var msg = (LOGIN_7_PROTOCOL.MSG_USER_VALIDATE)message.Message;

            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_USER_VALIDATE_RSP());
            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_STARTCHARACTERLIST());
            Context.Sender.Tell(new LOGIN_7_PROTOCOL.MSG_CHARACTERLIST());
        }

        private void ReceiveCreateCharacter(CommunicationDMLContext message)
        {
            var msg = (LOGIN_7_PROTOCOL.MSG_CREATECHARACTER)message.Message;
            var data = msg.CreationInfo;
        }
    }
}
