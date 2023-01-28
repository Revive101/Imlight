using Akka.Actor;
using Imlight.Common;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WizUnraveler;
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
        private const byte KEEP_ALIVE_INTERVAL = 10;     // In seconds
        private const byte KEEP_ALIVE_RSP_WAIT_TIME = 5; // In seconds

        public Socket Socket { get; init; }
        public ushort SessionID { get; init; }
        public bool IsSessionValid { get; private set; }
        public DateTime SessionStartTime { get; private set; }

        private IActorRef _prevSelf; // Weak solution.
        private bool _isSending;
        private bool _isWaitingForHeartbeatResponse;
        private readonly ServerReceiverActor _server;
        private readonly IActorRef _serverActor;
        private readonly SocketAsyncEventArgs _sendEventArgs = new SocketAsyncEventArgs();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public CommunicationActor(Socket Socket, ushort SessionID, ServerReceiverActor server)
        {
            this.Socket = Socket;
            this.SessionID = SessionID;
            this._server = server;
            this._serverActor = Context.Parent;

            _prevSelf = Self;

            ConfigureReceivers();
            SendSessionOffer();
            ListenAndProcess();
        }

        public static Props Props(Socket Socket, ushort SessionID, ServerReceiverActor server)
        {
            return Akka.Actor.Props.Create(() => new CommunicationActor(Socket, SessionID, server));
        }

        public void Send(INetworkMessage message)
        {
            if (!Socket.Connected)
            {
                Log.Logger.Error($"CommunicationActor [{SessionID}] send failure: " +
                    $"Socket is not connected!");
                return;
            }
            if (_isSending)
            {
                Log.Logger.Error($"CommunicationActor [{SessionID}] send failure: " +
                    $"Asynchronous send operation already in progress.");
                return;
            }

            var data = MessageSerializer.SerializeMessageBinary(message);
            _isSending = true;
            _sendEventArgs.SetBuffer(data, 0, data.Length);
            _sendEventArgs.UserToken = Socket;
            _sendEventArgs.Completed += SendEventArgs_Completed;

            var willRaiseEvent = Socket.SendAsync(_sendEventArgs);
            if (!willRaiseEvent)
            {
                SendEventArgs_Completed(this, _sendEventArgs);
            }

            var scopedMessageName = message
                .GetType()
                .ToString()
                .Split('.')[^1];
            Log.Logger.Verbose($"CommunicationActor [{SessionID}] send message [{scopedMessageName}]");
        }

        public void Close()
        {
            _cts.Cancel();
            _prevSelf.GracefulStop(TimeSpan.FromSeconds(1));
        }

        protected override void Unhandled(object message)
        {
            Log.Logger.Error($"CommunicationActor [{SessionID}] " +
                $"received unhandled message of type [{message.GetType()}].");
        }

        private void ConfigureReceivers()
        {
            // If we don't receive a KeepAliveRsp by now, drop this connection.
            Receive<string>(s => s == "KeepAliveHeartbeat", x => SendHeartbeat());
            Receive<string>(s => s == "KeepAliveEndTimes" && _isWaitingForHeartbeatResponse, x => Close());

            Receive<INetworkMessage>(x => Send(x));
        }

        private void ListenAndProcess()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var buffer = new byte[BUFFER_SIZE];

                try
                {
                    var bytesReceived = Socket.Receive(buffer);
                    if (bytesReceived <= 0)
                        continue;

                    var bufferSpan = new ReadOnlySpan<byte>(buffer, 0, bytesReceived).ToArray();
                    if (!IsKIPacket(bufferSpan) || !TryDeserializePacket(bufferSpan, out var record))
                    {
                        Log.Logger.Error($"CommunicationActor [{SessionID}] received non-KINP packet.");
                        continue;
                    }

                    // Log
                    var scopedMessageName = record
                        .GetType()
                        .ToString()
                        .Split('.')[^1];
                    Log.Logger.Verbose($"CommunicationActor [{SessionID}] received message [{scopedMessageName}]");

                    // If the message received is a control message, the CommunicationActor can handle it.
                    if (record.ServiceID == 0)
                        HandleControlMessage(record);
                    else
                        _serverActor.Tell(record, _prevSelf);
                }
                catch (SocketException ex)
                {
                    HandleError(ex);
                }
                catch (Exception ex)
                {
                    HandleError(ex);
                }
            }
        }

        private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            _isSending = false;
            if (e.SocketError != SocketError.Success)
            {
                Log.Logger.Error($"CommunicationActor [{SessionID}] send failure: {e.SocketError}");
                return;
            }
        }

        private void HandleError(Exception ex)
        {
            Log.Logger.Error($"CommunicationActor [{SessionID}] unhandled listen error: {ex.Message}");
            if (DISPOSE_ON_UNHANDLED_EXCEPTION)
            {
                Close();
            }
        }

        private void HandleError(SocketError error)
        {
            Log.Logger.Warning($"CommunicationActor [{SessionID}] socket error: {error}");
            Close();
        }

        private void HandleControlMessage(INetworkMessage message)
        {
            switch (message.MessageOrder)
            {
                case 3:
                    ReceiveKeepAlive((ControlMessages.KeepAlive)message);
                    break;
                case 4:
                    ReceiveKeepAliveRsp((ControlMessages.KeepAliveResponse)message);
                    break;
                case 5:
                    ReceiveSessionAccept((ControlMessages.SessionAccept)message);
                    break;
            }
        }

        private void SendSessionOffer()
        {
            var sessionOffer = new ControlMessages.SessionOffer()
            {
                SessionID = SessionID,
            };

            Send(sessionOffer);
        }

        private void SendHeartbeat()
        {
            if (!IsSessionValid)
            {
                Log.Logger.Error($"CommunicationActor [{SessionID}] tried to send heartbeat to an invalid session.");
                return;
            }

            // We're going to send a heartbeat to our connected session.
            // If we don't receive a response for `KEEP_ALIVE_RSP_WAIT_TIME`, we'll drop the session.
            _isWaitingForHeartbeatResponse = true;

            var keepAlive = new ControlMessages.KeepAliveServer()
            {
                SessionID = SessionID,
                Milliseconds = (uint)_server.ServerElapsed(),
            };

            Send(keepAlive);

            // Send message to self after x seconds to remind CommunicationActor to check
            // the status of that KeepAlive.
            var reminderTime = TimeSpan.FromSeconds(KEEP_ALIVE_RSP_WAIT_TIME);
            Context.System.Scheduler.ScheduleTellOnce(reminderTime, _prevSelf, "KeepAliveEndTimes", _prevSelf);
        }

        private void ReceiveSessionAccept(ControlMessages.SessionAccept message)
        {
            IsSessionValid = true;

            long unixTime = ((long)message.TimestampUpper << 32) | (uint)message.TimestampLower;
            SessionStartTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;

            // Once the session is created, we need to send a heartbeat to keep it active.
            var heartbeatInterval = TimeSpan.FromSeconds(KEEP_ALIVE_INTERVAL);
            Context.System.Scheduler.ScheduleTellRepeatedly(
                heartbeatInterval, 
                heartbeatInterval,
                Self, 
                "KeepAliveHeartbeat",
                ActorRefs.NoSender);

            // Log
            Log.Logger.Information($"CommunicationActor [{SessionID}] session created.");
        }

        private void ReceiveKeepAlive(ControlMessages.KeepAlive message)
        {
            if (message.SessionID != SessionID)
            {
                Log.Logger.Error($"CommunicationActor [{SessionID}] received misaligned Session ID. The connection will be dropped." +
                    $"\n\t\tReceived: {message.SessionID}");

                _prevSelf.GracefulStop(TimeSpan.FromSeconds(1));
                return;
            }

            var keepAliveRsp = new ControlMessages.KeepAliveResponse()
            {
                SessionID = SessionID, 
                Milliseconds = message.Milliseconds,
                ElapsedSessionTime = message.ElapsedSessionTime
            };
            Send(keepAliveRsp);
        }

        private void ReceiveKeepAliveRsp(ControlMessages.KeepAliveResponse message)
        {
            _isWaitingForHeartbeatResponse = false;
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
