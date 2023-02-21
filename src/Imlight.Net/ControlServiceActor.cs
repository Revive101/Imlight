using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Net.Messages;
using WizUnraveler.DML;
using Imlight.Common;
using System.Diagnostics;

namespace Imlight.Net
{
    public class ControlServiceActor : ActorMessageService
    {
        public override HashSet<Type> Messages { get; init; }
        public bool Valid;

        private SessionActor _parentActor;
        private Stopwatch _sessionOfferTime;
        private bool _isWaitingForHeartbeatResponse;

        public ControlServiceActor(SessionActor parentActor) : base()
        {
            this._parentActor = parentActor;
            this.Messages = new HashSet<Type>()
            {
                typeof(ControlMessages.SessionOffer),
                typeof(ControlMessages.SessionAccept),
                typeof(ControlMessages.KeepAlive),
                typeof(ControlMessages.KeepAliveResponse),
            };

            SendSessionOffer();
        }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new ControlServiceActor(parentActor));
        }

        protected override void ConfigureReceivers()
        {
            Receive<string>(x => x == ASK_IDENTIFY, x => Sender.Tell(new ServiceIdentityReply(this)));

            Receive<ControlMessages.SessionAccept>(x => ReceiveSessionAccept(x));
            Receive<ControlMessages.KeepAlive>(x => ReceiveKeepAlive(x));
            Receive<ControlMessages.KeepAliveResponse>(x => ReceiveKeepAliveRsp(x));
        }

        private void SendSessionOffer()
        {
            uint currentUnixTimestamp = (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            int timestampUpper = (int)(currentUnixTimestamp >> 32);
            int timestampLower = (int)(currentUnixTimestamp & uint.MaxValue);
            uint millisecondsIntoCurrentSecond = (uint)(DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);

            var offer = new ControlMessages.SessionOffer()
            {
                SessionID = _parentActor.SessionID,
                TimestampUpper = timestampUpper,
                TimestampLower = timestampLower,
                Milliseconds = millisecondsIntoCurrentSecond,
            };

            SendToParent(offer);

            // Start the stopwatch so we can later get RTT (ping).
            _sessionOfferTime.Restart();
        }

        private void ReceiveSessionAccept(ControlMessages.SessionAccept message)
        {
            _sessionOfferTime.Stop();
            if (message.SessionID != _parentActor.SessionID)
            {
                Log.Logger.Error($"SessionActor [{_parentActor.SessionID}] misaligned Session ID.");
                return;
            }

            Valid = true;

            Log.Logger.Information($"Session created with ID [{_parentActor.SessionID}] PING: [{_sessionOfferTime.ElapsedMilliseconds}]");
        }

        private void ReceiveKeepAlive(ControlMessages.KeepAlive message)
        {
            if (message.SessionID != _parentActor.SessionID)
            {
                Log.Logger.Error($"SessionActor [{_parentActor.SessionID}] misaligned Session ID.");
                return;
            }

            ushort millisecondsIntoCurrentSecond = (ushort)(DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);
            var rsp = new ControlMessages.KeepAliveResponse()
            {
                SessionID = _parentActor.SessionID,
                Milliseconds = millisecondsIntoCurrentSecond,
                ElapsedSessionTime = message.ElapsedSessionTime,
            };

            SendToParent(rsp);
        }

        private void ReceiveKeepAliveRsp(ControlMessages.KeepAliveResponse message)
        {
            _isWaitingForHeartbeatResponse = false;
        }

        private void SendToParent(INetworkMessage message)
        {
            if (_parentActor is null)
            {
                Log.Logger.Error($"ControlServiceActor attempted to send message to undefined SessionActor.");
                return;
            }

            _parentActor.Send(message);
        }
    }
}
