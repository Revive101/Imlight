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

namespace Imlight.Net.Services
{
    public class ControlService : MessageService
    {
        private const byte KEEP_ALIVE_INTERVAL = 60;       // In seconds
        private const byte KEEP_ALIVE_RSP_WAIT_TIME = 2;   // In seconds

        private bool _sessionValid;
        private readonly Stopwatch _responseStopwatch;
        private bool _isWaitingForHeartbeatResponse;
        private bool _halted;

        public ControlService(SessionActor parentActor) : base(parentActor)
        {
            this._responseStopwatch = new Stopwatch();

            SendSessionOffer();
        }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new ControlService(parentActor));
        }

        protected override void ConfigureReceivers()
        {
            // These are sent from self on interval to remind the actor of the session heartbeat.
            Receive<string>(s => s == "KeepAliveHeartbeat", x => SendHeartbeat());
            Receive<string>(s => s == "KeepAliveEndTimes", x => ReceiveKeepAliveEndTimes());

            base.ConfigureReceivers();
        }

        private void SendSessionOffer()
        {
            var currentUnixTimestamp = (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            var timestampUpper = (int)(currentUnixTimestamp >> 32);
            var timestampLower = (int)(currentUnixTimestamp & uint.MaxValue);
            var millisecondsIntoCurrentSecond = (uint)(DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);

            var offer = new ControlMessages.SessionOffer()
            {
                SessionID = SessionActor.SessionID,
                TimestampUpper = timestampUpper,
                TimestampLower = timestampLower,
                Milliseconds = millisecondsIntoCurrentSecond,
            };

            SendToSocket(offer);

            // Start the stopwatch so we can later get RTT (ping).
            _responseStopwatch.Restart();
        }

        [MessageHandler(typeof(ControlMessages.SessionAccept))]
        private void ReceiveSessionAccept(ControlMessages.SessionAccept message)
        {
            _responseStopwatch.Stop();
            if (message.SessionID != SessionActor.SessionID)
            {
                Log.Logger.Error($"SessionActor [{SessionActor.SessionID}] misaligned Session ID.");
                SendCloseSession();
                return;
            }

            // Set local variables.
            _sessionValid = true;
            _isWaitingForHeartbeatResponse = false;

            // The session is now valid. For optimization purposes, our parent SessionActor doesn't load
            // all the services on creation. Instead, we wait for the session to be valid.
            // We need to now tell our SessionActor that the session is created, and to grab the rest of its services.
            SessionActor.InitializeActiveSession();
            SessionActor
                .ActorRef
                .Tell(new SERVER_100_PROTOCOL.MSG_PING() {Ping = _responseStopwatch.ElapsedMilliseconds});

            // Once the session is created, we need to send a heartbeat to keep it active.
            // To do that. we'll have this actor send a message to itself on interval to check on the heartbeat.
            var heartbeatInterval = TimeSpan.FromSeconds(KEEP_ALIVE_INTERVAL);
            Context.System.Scheduler.ScheduleTellRepeatedly(
                heartbeatInterval,
                heartbeatInterval,
                Context.Self,
                "KeepAliveHeartbeat",
                Context.Self);
            
            _responseStopwatch.Reset();
        }

        [MessageHandler(typeof(ControlMessages.KeepAlive))]
        private void ReceiveKeepAlive(ControlMessages.KeepAlive message)
        {
            if (message.SessionID != SessionActor.SessionID)
            {
                Log.Logger.Error($"SessionActor [{SessionActor.SessionID}] misaligned Session ID.");
                SendCloseSession();

                return;
            }

            var millisecondsIntoCurrentSecond = (ushort)(DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);
            var rsp = new ControlMessages.KeepAliveResponse()
            {
                SessionID = SessionActor.SessionID,
                Milliseconds = millisecondsIntoCurrentSecond,
                ElapsedSessionTime = message.ElapsedSessionTime,
            };

            SendToSocket(rsp);
        }

        [MessageHandler(typeof(ControlMessages.KeepAliveResponse))]
        private void ReceiveKeepAliveRsp(ControlMessages.KeepAliveResponse message)
        {
            _responseStopwatch.Reset();
            _isWaitingForHeartbeatResponse = false;
            
            SessionActor
                .ActorRef
                .Tell(new SERVER_100_PROTOCOL.MSG_PING() {Ping = _responseStopwatch.ElapsedMilliseconds});
        }

        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT))]
        private void ReceiveHalt(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT message) => _halted = true;

        [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME))]
        private void ReceiveResume(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME message) => _halted = false;

        private void SendHeartbeat()
        {
            // If this service is halted, it means the session has moved onto the game server.
            // The login socket is now closed, and we don't need to send heartbeats anymore.
            if (_halted) return;
            if (!_sessionValid)
            {
                Log.Logger.Error($"CommunicationActor [{SessionActor.SessionID}] " +
                                 $"tried to send heartbeat to an invalid session.");
                SendCloseSession();
                return;
            }

            // We're going to send a heartbeat to our connected session.
            // If we don't receive a response for `KEEP_ALIVE_RSP_WAIT_TIME` time, we'll drop the session.
            _isWaitingForHeartbeatResponse = true;

            var keepAlive = new ControlMessages.KeepAliveServer()
            {
                SessionID = SessionActor.SessionID,
                Milliseconds = (uint)0,
            };

            SendToSocket(keepAlive);

            // Send message to self after x seconds to remind CommunicationActor to check
            // the status of the KeepAlive.
            var reminderTime = TimeSpan.FromSeconds(KEEP_ALIVE_RSP_WAIT_TIME);
            Context.System.Scheduler.ScheduleTellOnce(
                reminderTime, 
                Context.Self, 
                "KeepAliveEndTimes", 
                Context.Self);
            
            _responseStopwatch.Start();
        }

        private void ReceiveKeepAliveEndTimes()
        {
            if (!_isWaitingForHeartbeatResponse || _halted) return;

            SendCloseSession();
        }
    }
}
