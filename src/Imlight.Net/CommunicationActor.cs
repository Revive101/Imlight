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

        public Socket Socket { get; init; }
        public ushort SessionID { get; init; }

        //@todo: Add PlayerActor or LoginUserActor.

        private bool _isOpen;
        private bool _sessionValid;
        private IActorRef _serverActor;
        private IActorRef _prevSelf; // Weak solution.
        private bool _isSending;
        private readonly SocketAsyncEventArgs _sendEventArgs = new SocketAsyncEventArgs();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public CommunicationActor(Socket Socket, ushort SessionID)
        {
            this.Socket = Socket;
            this.SessionID = SessionID;
            this._serverActor = Context.Parent;
            this._isOpen = true;

            SendSessionOffer();
            Become(ListenAndProcess);
        }

        public static Props Props(Socket Socket, ushort SessionID)
        {
            return Akka.Actor.Props.Create(() => new CommunicationActor(Socket, SessionID));
        }

        private void ListenAndProcess()
        {
            this._prevSelf = Self;
            ConfigureReceivers();

            while (!_cts.Token.IsCancellationRequested)
            {
                var buffer = new byte[BUFFER_SIZE];

                try
                {
                    using var socketEventArgs = new SocketAsyncEventArgs();
                    socketEventArgs.SetBuffer(buffer, 0, buffer.Length);
                    socketEventArgs.UserToken = Socket;
                    socketEventArgs.SocketFlags = SocketFlags.None;
                    socketEventArgs.Completed += ProcessReceive;

                    if (!Socket.ReceiveAsync(socketEventArgs))
                    {
                        ProcessReceive(null, socketEventArgs);
                    }
                }
                catch (Exception ex)
                {
                    HandleError(ex);
                }
            }

            Log.Logger.Warning($"CommunicationActor [{SessionID}] closed and listen loop stopped.");
        }

        private void ProcessReceive(object sender, SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                var buffer = new ReadOnlySpan<byte>(e.Buffer, 0, e.BytesTransferred).ToArray();
                if (!IsKIPacket(buffer) || !TryDeserializePacket(buffer, out var record))
                {
                    Log.Logger.Error($"CommunicationActor [{SessionID}] received non-KINP packet.");
                    return;
                }

                // Log
                var scopedMessageName = record
                    .GetType()
                    .ToString()
                    .Split('.')[^1];
                Log.Logger.Verbose($"CommunicationActor [{SessionID}] received message [{scopedMessageName}]");

                // Return the decoded data to the ServerReceiverActor.
                _serverActor.Tell(record, _prevSelf);
            }
            else if (e.SocketError != SocketError.Success)
            {
                HandleError(e.SocketError);
            }
        }

        private void HandleError(Exception ex)
        {
            Log.Logger.Error($"CommunicationActor [{SessionID}] unhandled listen error: {ex.Message}");
            if (DISPOSE_ON_UNHANDLED_EXCEPTION)
            {
                _isOpen = false;
                _cts.Cancel();
                _prevSelf.GracefulStop(TimeSpan.FromSeconds(1));
            }
        }

        private void HandleError(SocketError error)
        {
            Log.Logger.Warning($"CommunicationActor [{SessionID}] socket error: {error}");
            _isOpen = false;
            _cts.Cancel();
            _prevSelf.GracefulStop(TimeSpan.FromSeconds(1));
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

        private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            _isSending = false;
            if (e.SocketError != SocketError.Success)
            {
                Log.Logger.Error($"CommunicationActor [{SessionID}] send failure: {e.SocketError}");
                return;
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

        private void ConfigureReceivers()
        {
            Receive<INetworkMessage>(x => Task.Run(() => Send(x)));
        }

        protected override void Unhandled(object message)
        {
            Log.Logger.Error($"CommunicationActor [{SessionID}] " +
                $"received unhandled message of type [{message.GetType()}].");
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
