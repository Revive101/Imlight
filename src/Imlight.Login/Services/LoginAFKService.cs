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

        private bool _halted;
        private long _lastReceivedSeconds;
        private readonly Timer _timer;

        public LoginAFKService(SessionActor parentActor) : base (parentActor)
        {
            _timer = new Timer(AFK_CHECK_INTERVAL * 1000);
            _timer.Elapsed += CheckAfk;
            _timer.AutoReset = true;
            _timer.Enabled = true;
        }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new LoginAFKService(parentActor));
        }

        [MessageHandler(typeof(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK))]
        private void ReceiveLoginNotAFK(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK message)
        {
            _lastReceivedSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT))]
        private void ReceiveHalt(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT message)
        {
            _halted = true;
        }
        
        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME))]
        private void ReceiveResume(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME message)
        {
            _halted = false;
        }

        private void CheckAfk(object sender, ElapsedEventArgs e)
        {
            if (_halted) return;
            
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (currentTime - _lastReceivedSeconds >= AFK_TIMEOUT)
            {
                // User has gone AFK. Drop the connection.
                SendToSocket(new LOGIN_7_PROTOCOL.MSG_DISCONNECT_LOGIN_AFK()
                {
                    Warning = 1 // ???
                });

                CloseSession();
            }
        }
    }
}
