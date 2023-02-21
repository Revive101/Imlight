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
        private const byte KEEP_ALIVE_INTERVAL = 60;       // In seconds
        private const byte KEEP_ALIVE_RSP_WAIT_TIME = 2;   // In seconds
        private const byte KEEP_ALIVE_REVIVE_ATTEMPTS = 3; // The amount of times the server will try to revive a connection.

        public override HashSet<Type> Messages { get; init; }
        public bool Valid;

        private SessionActor _parentActor;
        private Stopwatch _sessionOfferTime;
        private bool _isWaitingForHeartbeatResponse;
        private byte _currentKeepAliveReviveAttempts;

        public ControlServiceActor(SessionActor parentActor) : base()
        {
            this._parentActor = parentActor;
            this._sessionOfferTime = new Stopwatch();
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

            Receive<string>(s => s == "KeepAliveHeartbeat", x => SendHeartbeat());
            Receive<string>(s => s == "KeepAliveEndTimes", x => ReceiveKeepAliveEndTimes());
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
                _parentActor.Close();
                return;
            }

            Valid = true;
            _isWaitingForHeartbeatResponse = false;
            _currentKeepAliveReviveAttempts = 0;

            // Once the session is created, we need to send a heartbeat to keep it active.
            var heartbeatInterval = TimeSpan.FromSeconds(KEEP_ALIVE_INTERVAL);
            Context.System.Scheduler.ScheduleTellRepeatedly(
                heartbeatInterval,
                heartbeatInterval,
                Context.Self,
                "KeepAliveHeartbeat",
                Context.Self);

            Log.Logger.Information($"Session created with ID [{_parentActor.SessionID}] PING: [{_sessionOfferTime.ElapsedMilliseconds}]");
        }

        private void ReceiveKeepAlive(ControlMessages.KeepAlive message)
        {
            if (message.SessionID != _parentActor.SessionID)
            {
                Log.Logger.Error($"SessionActor [{_parentActor.SessionID}] misaligned Session ID.");
                _parentActor.Close();
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
            _currentKeepAliveReviveAttempts = 0;
        }

        private void SendHeartbeat()
        {
            if (!Valid)
            {
                Log.Logger.Error($"CommunicationActor [{_parentActor.SessionID}] tried to send heartbeat to an invalid session.");
                _parentActor.Close();
                return;
            }

            // We're going to send a heartbeat to our connected session.
            // If we don't receive a response for `KEEP_ALIVE_RSP_WAIT_TIME` time, we'll drop the session.
            _isWaitingForHeartbeatResponse = true;

            var keepAlive = new ControlMessages.KeepAliveServer()
            {
                SessionID = _parentActor.SessionID,
                Milliseconds = (uint)0,
            };

            SendToParent(keepAlive);

            // Send message to self after x seconds to remind CommunicationActor to check
            // the status of that KeepAlive.
            var reminderTime = TimeSpan.FromSeconds(KEEP_ALIVE_RSP_WAIT_TIME);
            Context.System.Scheduler.ScheduleTellOnce(reminderTime, Self, "KeepAliveEndTimes", Self);
        }

        private void ReceiveKeepAliveEndTimes()
        {
            if (_isWaitingForHeartbeatResponse)
            {
                if (_currentKeepAliveReviveAttempts == KEEP_ALIVE_REVIVE_ATTEMPTS)
                {
                    _parentActor.Close();
                }
                else
                {
                    SendSessionOffer();
                    _currentKeepAliveReviveAttempts++;
                }
            }
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
