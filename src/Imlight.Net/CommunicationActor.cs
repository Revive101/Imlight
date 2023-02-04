using Akka.Actor;
using Imlight.Common;
using Imlight.Net.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.DML;

namespace Imlight.Net
{
    /// <summary>
    /// The CommunicationActor is resposible for managing a socket connection and communication.
    /// </summary>
    public class CommunicationActor : ReceiveActor
    {
        private const bool DISPOSE_ON_UNHANDLED_EXCEPTION = true;
        private const int BUFFER_SIZE = 4096;
        private const byte KEEP_ALIVE_INTERVAL = 60;       // In seconds
        private const byte KEEP_ALIVE_RSP_WAIT_TIME = 10;  // In seconds
        private const byte KEEP_ALIVE_REVIVE_ATTEMPTS = 3; // The amount of times the server will try to revive a connection.

        public Session Session { get; private set; }

        private bool _isSending;
        private bool _isWaitingForHeartbeatResponse;
        private byte _currentKeepAliveReviveAttempts;
        private readonly ServerReceiverActor _server;
        private readonly IActorRef _serverActor;
        private readonly SocketAsyncEventArgs _sendEventArgs = new SocketAsyncEventArgs();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public CommunicationActor(Session session, ServerReceiverActor server)
        {
            this.Session = session;
            this.Session.Valid = false;
            this._server = server;
            this._serverActor = Context.Parent;

            var self = Context;

            ConfigureReceivers();
            SendSessionOffer();

            Task.Factory.StartNew(() => ListenAndProcess(self));
        }

        public static Props Props(Session session, ServerReceiverActor server)
        {
            return Akka.Actor.Props.Create(() => new CommunicationActor(session, server));
        }

        public void Send(INetworkMessage message)
        {
            if (!Session.Socket.Connected)
            {
                Log.Logger.Error($"CommunicationActor [{Session.SessionID}] send failure: " +
                    $"Socket is not connected!");
                return;
            }
            if (_isSending)
            {
                Log.Logger.Error($"CommunicationActor [{Session.SessionID}] send failure: " +
                    $"Asynchronous send operation already in progress.");
                return;
            }

            var data = MessageSerializer.SerializeMessageBinary(message);
            _isSending = true;
            _sendEventArgs.SetBuffer(data, 0, data.Length);
            _sendEventArgs.UserToken = Session.Socket;
            _sendEventArgs.Completed += SendEventArgs_Completed;

            var willRaiseEvent = Session.Socket.SendAsync(_sendEventArgs);
            if (!willRaiseEvent)
            {
                SendEventArgs_Completed(this, _sendEventArgs);
            }

            var scopedMessageName = message
                .GetType().ToString().Split('.')[^1];
            Log.Logger.Verbose($"CommunicationActor [{Session.SessionID}] send message [{scopedMessageName}]");
        }

        public void Close()
        {
            Session.Socket.Close();
            _cts.Cancel();
            Context.Stop(Self);
        }

        protected override void Unhandled(object message)
        {
            Log.Logger.Error($"CommunicationActor [{Session.SessionID}] " +
                $"received unhandled message of type [{message.GetType()}].");
        }

        private void ConfigureReceivers()
        {
            Receive<string>(s => s == "KeepAliveHeartbeat", x => SendHeartbeat());
            Receive<string>(s => s == "KeepAliveEndTimes", x => ReceiveKeepAliveEndTimes());
            Receive<string>(s => s == "Close", x => Close());

            Receive<INetworkMessage>(x => Send(x));
        }

        private void ListenAndProcess(IUntypedActorContext context)
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var buffer = new byte[BUFFER_SIZE];
                    var bytesReceived = Session.Socket.Receive(buffer);
                    if (bytesReceived <= 0) continue;

                    var packet = GetPacketFromBuffer(buffer, bytesReceived);
                    if (packet == null) continue;

                    HandlePacket(packet, context);
                }
                catch (SocketException ex)
                {
                    Log.Logger.Error($"CommunicationActor [{Session.SessionID}] socket error: {ex.Message}");
                    break;
                }
            }

            context.Self.Tell("Close");
        }

        private void HandlePacket(INetworkMessage packet, IUntypedActorContext context)
        {
            // Log the incoming packet.
            var scopedMessageName = packet.GetType().ToString().Split('.')[^1];
            Log.Logger.Verbose($"CommunicationActor [{Session.SessionID}] received message [{scopedMessageName}]");

            if (packet.ServiceID == 0)
                HandleControlMessage(packet, context);
            else if (packet.GetType() == typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))
                _cts.Cancel();
            else
            {
                // Craft context & send to server.
                _serverActor.Tell(packet, context.Self);
            }
        }

        private INetworkMessage GetPacketFromBuffer(byte[] buffer, int bytesReceived)
        {
            var bufferSpan = new ReadOnlySpan<byte>(buffer, 0, bytesReceived).ToArray();
            if (!IsKIPacket(bufferSpan) || !TryDeserializePacket(bufferSpan, out var record))
            {
                Log.Logger.Error($"CommunicationActor [{Session.SessionID}] received non-KINP packet.");
                return null;
            }

            return record;
        }

        private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            _isSending = false;
            if (e.SocketError != SocketError.Success)
            {
                Log.Logger.Error($"CommunicationActor [{Session.SessionID}] send failure: {e.SocketError}");
                return;
            }
        }

        private void HandleControlMessage(INetworkMessage message, IUntypedActorContext context)
        {
            switch (message.MessageOrder)
            {
                case 3:
                    ReceiveKeepAlive((ControlMessages.KeepAlive)message, context);
                    break;
                case 4:
                    ReceiveKeepAliveRsp((ControlMessages.KeepAliveResponse)message, context);
                    break;
                case 5:
                    ReceiveSessionAccept((ControlMessages.SessionAccept)message, context);
                    break;
            }
        }

        private void SendSessionOffer()
        {
            uint currentUnixTimestamp = (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            int timestampUpper = (int)(currentUnixTimestamp >> 32);
            int timestampLower = (int)(currentUnixTimestamp & uint.MaxValue);
            uint millisecondsIntoCurrentSecond = (uint)(DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);

            Session.SessionStartTime = currentUnixTimestamp;
            Session.SessionMilliseconds = millisecondsIntoCurrentSecond;

            var sessionOffer = new ControlMessages.SessionOffer() 
            { 
                SessionID = Session.SessionID,
                TimestampUpper = timestampUpper,
                TimestampLower = timestampLower,
                Milliseconds = millisecondsIntoCurrentSecond,
            };
            Send(sessionOffer);
        }

        private void SendHeartbeat()
        {
            if (!Session.Valid)
            {
                Log.Logger.Error($"CommunicationActor [{Session.SessionID}] tried to send heartbeat to an invalid session.");
                return;
            }

            // We're going to send a heartbeat to our connected session.
            // If we don't receive a response for `KEEP_ALIVE_RSP_WAIT_TIME` time, we'll drop the session.
            _isWaitingForHeartbeatResponse = true;

            var keepAlive = new ControlMessages.KeepAliveServer()
            {
                SessionID = Session.SessionID,
                Milliseconds = (uint)_server.ServerElapsed(),
            };

            Send(keepAlive);

            // Send message to self after x seconds to remind CommunicationActor to check
            // the status of that KeepAlive.
            var reminderTime = TimeSpan.FromSeconds(KEEP_ALIVE_RSP_WAIT_TIME);
            Context.System.Scheduler.ScheduleTellOnce(reminderTime, Self, "KeepAliveEndTimes", Self);
        }

        private void ReceiveKeepAlive(ControlMessages.KeepAlive message, IUntypedActorContext context)
        {
            if (message.SessionID != Session.SessionID)
            {
                Log.Logger.Error($"CommunicationActor [{Session.SessionID}] received misaligned Session ID. The connection will be dropped." +
                    $"\n\t\tReceived: {message.SessionID}");

                Close();
                return;
            }

            var keepAliveRsp = new ControlMessages.KeepAliveResponse()
            {
                SessionID = Session.SessionID, 
                Milliseconds = message.Milliseconds,
                ElapsedSessionTime = message.ElapsedSessionTime
            };
            Send(keepAliveRsp);
        }

        private void ReceiveKeepAliveRsp(ControlMessages.KeepAliveResponse message, IUntypedActorContext context)
        {
            _isWaitingForHeartbeatResponse = false;
            _currentKeepAliveReviveAttempts = 0;
        }

        private void ReceiveSessionAccept(ControlMessages.SessionAccept message, IUntypedActorContext context)
        {
            _isWaitingForHeartbeatResponse = false;
            _currentKeepAliveReviveAttempts = 0;
            Session.Valid = true;

            // @TODO: Add this to message `ClientConnected` to get RTT.
            //uint unixTime = ((uint)message.TimestampUpper << 32) | (uint)message.TimestampLower;

            _serverActor.Tell(new ClientConnected(Session.Socket));

            // Once the session is created, we need to send a heartbeat to keep it active.
            var heartbeatInterval = TimeSpan.FromSeconds(KEEP_ALIVE_INTERVAL);
            context.System.Scheduler.ScheduleTellRepeatedly(
                heartbeatInterval,
                heartbeatInterval,
                context.Self,
                "KeepAliveHeartbeat",
                context.Self);
        }

        private void ReceiveKeepAliveEndTimes()
        {
            if (_isWaitingForHeartbeatResponse)
            {
                if (_currentKeepAliveReviveAttempts == KEEP_ALIVE_REVIVE_ATTEMPTS)
                {
                    Close();
                }
                else
                {
                    SendSessionOffer();
                    _currentKeepAliveReviveAttempts++;
                }
            }
        }

        private bool TryDeserializePacket(byte[] buffer, out INetworkMessage message)
        {
            try
            {
                message = MessageSerializer.DeserializeMessageBinary(buffer);
                return true;
            }
            catch
            {
                message = null;
                return false;
            }
        }

        private bool IsKIPacket(byte[] buffer)
            => (buffer.AsSpan()[0..2].SequenceEqual(stackalloc byte[2] { 0x0D, 0xF0 }));
    }
}
