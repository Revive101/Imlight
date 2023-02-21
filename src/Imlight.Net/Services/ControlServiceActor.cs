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
    public class ControlServiceActor : MessageService
    {
        private const byte KEEP_ALIVE_INTERVAL = 60;       // In seconds
        private const byte KEEP_ALIVE_RSP_WAIT_TIME = 2;   // In seconds
        private const byte KEEP_ALIVE_REVIVE_ATTEMPTS = 3; // The amount of times the server will try to revive a connection.

        public bool Valid;

        private Stopwatch _sessionOfferTime;
        private bool _isWaitingForHeartbeatResponse;
        private byte _currentKeepAliveReviveAttempts;

        public ControlServiceActor(SessionActor parentActor) : base()
        {
            this.SessionActor = parentActor;
            this._sessionOfferTime = new Stopwatch();
            SendSessionOffer();
        }

        protected static Props Props(SessionActor parentActor)
        {
            return Akka.Actor.Props.Create(() => new ControlServiceActor(parentActor));
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
            uint currentUnixTimestamp = (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            int timestampUpper = (int)(currentUnixTimestamp >> 32);
            int timestampLower = (int)(currentUnixTimestamp & uint.MaxValue);
            uint millisecondsIntoCurrentSecond = (uint)(DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);

            var offer = new ControlMessages.SessionOffer()
            {
                SessionID = SessionActor.SessionID,
                TimestampUpper = timestampUpper,
                TimestampLower = timestampLower,
                Milliseconds = millisecondsIntoCurrentSecond,
            };

            SendToParent(offer);

            // Start the stopwatch so we can later get RTT (ping).
            _sessionOfferTime.Restart();
        }

        [MessageHandler(typeof(ControlMessages.SessionAccept))]
        private void ReceiveSessionAccept(ControlMessages.SessionAccept message)
        {
            _sessionOfferTime.Stop();
            if (message.SessionID != SessionActor.SessionID)
            {
                Log.Logger.Error($"SessionActor [{SessionActor.SessionID}] misaligned Session ID.");
                SessionActor.Close();
                return;
            }

            // Set local variables.
            Valid = true;
            _isWaitingForHeartbeatResponse = false;
            _currentKeepAliveReviveAttempts = 0;

            // The session is now valid. For optimization purposes, our parent SessionActor doesn't load
            // all the services on creation. Instead, we wait for the session to be valid.
            // We need to now tell our SessionActor that the session is created, and to grab the rest of its services.
            SessionActor.FullInitialize(_sessionOfferTime.ElapsedMilliseconds);

            // Once the session is created, we need to send a heartbeat to keep it active.
            // To do that. we'll have this class send a message to itself on interval to check on the heartbeat.
            var heartbeatInterval = TimeSpan.FromSeconds(KEEP_ALIVE_INTERVAL);
            Context.System.Scheduler.ScheduleTellRepeatedly(
                heartbeatInterval,
                heartbeatInterval,
                Context.Self,
                "KeepAliveHeartbeat",
                Context.Self);
        }

        [MessageHandler(typeof(ControlMessages.KeepAlive))]
        private void ReceiveKeepAlive(ControlMessages.KeepAlive message)
        {
            if (message.SessionID != SessionActor.SessionID)
            {
                Log.Logger.Error($"SessionActor [{SessionActor.SessionID}] misaligned Session ID.");
                SessionActor.Close();
                return;
            }

            ushort millisecondsIntoCurrentSecond = (ushort)(DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);
            var rsp = new ControlMessages.KeepAliveResponse()
            {
                SessionID = SessionActor.SessionID,
                Milliseconds = millisecondsIntoCurrentSecond,
                ElapsedSessionTime = message.ElapsedSessionTime,
            };

            SendToParent(rsp);
        }

        [MessageHandler(typeof(ControlMessages.KeepAliveResponse))]
        private void ReceiveKeepAliveRsp(ControlMessages.KeepAliveResponse message)
        {
            _isWaitingForHeartbeatResponse = false;
            _currentKeepAliveReviveAttempts = 0;
        }

        private void SendHeartbeat()
        {
            if (!Valid)
            {
                Log.Logger.Error($"CommunicationActor [{SessionActor.SessionID}] tried to send heartbeat to an invalid session.");
                SessionActor.Close();
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
                    SessionActor.Close();
                }
                else
                {
                    SendSessionOffer();
                    _currentKeepAliveReviveAttempts++;
                }
            }
        }
    }
}
