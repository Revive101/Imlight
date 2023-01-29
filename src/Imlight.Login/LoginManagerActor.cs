using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net;
using WizUnraveler.Cache;

namespace Imlight.Login
{
    public class LoginManagerActor : ServerReceiverActor
    {
        public LoginManagerActor(string Name, sbyte ID, ushort port) : base(Name, ID, port) { }

        public static Props Props(string Name, sbyte ID, ushort port)
        {
            return Akka.Actor.Props.Create(() => new LoginManagerActor(Name, ID, port));
        }

        protected override void ConfigureReceivers()
        {
            base.ConfigureReceivers();

            Receive<LOGIN_7_PROTOCOL.MSG_USER_VALIDATE>(x => ReceiveUserValidate(x));
        }

        private void ReceiveUserValidate(LOGIN_7_PROTOCOL.MSG_USER_VALIDATE message)
        {
            var actor = (CommunicationActor)Context.Sender;
            var test = Crypto.PassKey3(message.PassKey3, actor.SessionID, actor.SessionMilliseconds);
            Log.Logger.Information("t");
        }
    }
}
