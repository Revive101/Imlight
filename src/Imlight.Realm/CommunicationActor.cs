using Akka.Actor;
using Imlight.Common;
using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using WizUnraveler;
using WizUnraveler.DML;
using WizUnraveler.Cache;
using Imlight.Realm.Messages;

namespace Imlight.Realm
{
    /// <summary>
    /// The CommunicationActor is resposible for managing a socket connection and communication.
    /// </summary>
    internal class CommunicationActor : ReceiveActor
    {
        private const bool DISPOSE_ON_UNHANDLED_EXCEPTION = true;
        private const int BUFFER_SIZE = 4096;

        public Socket Socket { get; init; }
        public ushort SessionID { get; init; }
        // Set-once properties.
        public bool SessionAgreed 
        { 
            get 
            { 
                return _sessionAgreed; 
            } 
            set 
            { 
                if (!_sessionAgreed) _sessionAgreed = true; 
            } 
        }
        public bool WaitingForSessionAgreement 
        { 
            get 
            { 
                return _waitingForSessionAgreement;  
            } 
            set 
            { 
                if (!_waitingForSessionAgreement) _waitingForSessionAgreement = value; 
            } 
        }

        //@todo: Add PlayerActor or LoginUserActor.

        private bool _isOpen;
        private bool _sessionAgreed;
        private bool _waitingForSessionAgreement;
        private IActorRef _realmActor;
        private IActorRef _prevSelf; // Weak solution.
        private bool _isSending;
        private readonly SocketAsyncEventArgs _sendEventArgs = new SocketAsyncEventArgs();

        public CommunicationActor(Socket Socket, ushort SessionID, IActorRef realmActor)
        {
            this.Socket = Socket;
            this.SessionID = SessionID;
            this._realmActor = realmActor;
            this._isOpen = true;

            Log.Logger.Information($"CommunicationActor [{SessionID}] created for connection [{Socket.RemoteEndPoint}]");

            Become(ListenAndProcess);
        }

        public static Props Props(Socket Socket, ushort SessionID, IActorRef realmActor)
        {
            return Akka.Actor.Props.Create(() => new CommunicationActor(Socket, SessionID, realmActor));
        }

        private void ListenAndProcess()
        {
            this._prevSelf = Self;
            ConfigureReceivers();

            while (_isOpen && Socket.Connected)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);

                try
                {
                    using (var socketEventArgs = new SocketAsyncEventArgs())
                    {
                        socketEventArgs.SetBuffer(buffer, 0, buffer.Length);
                        socketEventArgs.UserToken = Socket;
                        socketEventArgs.SocketFlags = SocketFlags.None;
                        socketEventArgs.Completed += ProcessReceive;

                        if (!Socket.ReceiveAsync(socketEventArgs))
                        {
                            ProcessReceive(null, socketEventArgs);
                        }
                    }
                }
                catch (IOException)
                {
                    Log.Logger.Warning($"CommunicationActor [{SessionID}] connection forcibly closed by remote host. Dropping client.");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Logger.Error($"CommunicationActor [{SessionID}] unhandled listen error: {ex.Message}");
                    if (DISPOSE_ON_UNHANDLED_EXCEPTION) break;
                }
                finally
                {
                    _isOpen = false;
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        private void ProcessReceive(object sender, SocketAsyncEventArgs e)
        {
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                var buffer = e.Buffer;
                var receivedBytes = e.BytesTransferred;

                // Process the received data.
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
                Log.Logger.Debug($"CommunicationActor [{SessionID}] received message [{scopedMessageName}]");

                // If this session still is not set, ignore the first message and do handshake.
                if (!SessionAgreed)
                {
                    SendHandshake();
                    return;
                }

                _realmActor.Tell(record, _prevSelf);
            }
            else if (e.SocketError != SocketError.Success)
            {
                Log.Logger.Warning($"CommunicationActor [{SessionID}] error: {e.SocketError}");
                _isOpen = false;
                _prevSelf.GracefulStop(TimeSpan.FromSeconds(1));
            }
        }

        public async Task SendAsync(INetworkMessage message)
        {
            if (_isSending)
            {
                Log.Logger.Error($"CommunicationActor [{SessionID}] send failure: " +
                    $"Asynchronous send operation already in progress.");
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
            Log.Logger.Debug($"CommunicationActor [{SessionID}] send message [{scopedMessageName}]");
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

        private void ConfigureReceivers()
        {
            Receive<INetworkMessage>(x => Task.Run(() => SendAsync(x)));
        }

        private async void SendHandshake()
        {
            var offer = new ControlMessages.SessionOffer()
            {
                SessionID = SessionID,
                Unknown1 = 0,
            };
            await SendAsync(offer);
            SessionAgreed = true;
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
