using Akka.Actor;
using Imlight.Common;
using Imlight.Data;
using Imlight.Net;
using Imlight.Net.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.ObjectProperty;

namespace Imlight.Login.Services
{
    internal class LoginAFKService : MessageService
    {
        private const ushort AFK_TIMEOUT = 360;       // In seconds
        private const ushort AFK_CHECK_INTERVAL = 60; // In seconds

        private long _lastReceivedSeconds;
        private readonly Timer _timer;

        public LoginAFKService(SessionActor parentActor) : base (parentActor)
        {
            _timer = new Timer(AFK_CHECK_INTERVAL * 1000);
            _timer.Elapsed += CheckAFK;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new LoginAFKService(parentActor));
        }

        protected override void ConfigureReceivers()
        {
            base.ConfigureReceivers();

            Receive<string>(x => x == "AFKHeartbeat", x => ReceiveAfkHeartbeat());
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK))]
        private void ReceiveLoginNotAFK(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK message)
        {
            _lastReceivedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private void CheckAFK(object sender, ElapsedEventArgs e)
        {
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (currentTime - _lastReceivedSeconds >= AFK_TIMEOUT)
            {
                // User has gone AFK. Drop the connection.
                SendToSocket(new LOGIN_7_PROTOCOL.MSG_DISCONNECT_LOGIN_AFK()
                {
                    Warning = 1 // ???
                });

                SendCloseSession();
            }
        }
    }
}
