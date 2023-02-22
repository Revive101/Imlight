using Akka.Actor;
using Imlight.Common;
using Imlight.Net.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WizUnraveler;
using WizUnraveler.DML;

namespace Imlight.Net
{
    public class SessionActor : ReceiveActor
    {
        private const bool DISPOSE_ON_UNHANDLED_EXCEPTION = true;
        private const int BUFFER_SIZE = 4096;

        public ushort SessionID { get; init; }

        private readonly Socket _socket;
        private readonly IActorRef _actorFactoryRef;
        private readonly Dictionary<IActorRef, MessageService> _services;
        private readonly SocketAsyncEventArgs _sendEventArgs = new SocketAsyncEventArgs();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isSending;

        public SessionActor(Socket socket, ushort sessionId, IActorRef actorFactoryRef)
        {
            this._socket = socket;
            this.SessionID = sessionId;
            this._actorFactoryRef = actorFactoryRef;
            this._services = new Dictionary<IActorRef, MessageService>();

            var self = Context.Self;

            ConfigureReceivers();

            Task.Factory.StartNew(() => ListenAndProcess(self));
        }

        public static Props Props(Socket socket, ushort sessionId, IActorRef actorFactoryRef)
        {
            return Akka.Actor.Props.Create(() => new SessionActor(socket, sessionId, actorFactoryRef));
        }

        public void Send(INetworkMessage message)
        {
            if (!_socket.Connected)
            {
                Log.Logger.Error($"SessionActor [{SessionID}] send failure: " +
                    $"Socket is not connected!");
                return;
            }
            if (_isSending)
            {
                Log.Logger.Error($"SessionActor [{SessionID}] send failure: " +
                    $"Asynchronous send operation already in progress.");
                return;
            }

            var data = MessageSerializer.SerializeMessageBinary(message);
            _isSending = true;
            _sendEventArgs.SetBuffer(data, 0, data.Length);
            _sendEventArgs.UserToken = _socket;
            _sendEventArgs.Completed += SendEventArgs_Completed;

            var willRaiseEvent = _socket.SendAsync(_sendEventArgs);
            if (!willRaiseEvent)
            {
                SendEventArgs_Completed(this, _sendEventArgs);
            }

            var scopedMessageName = message
                .GetType().ToString().Split('.')[^1];
            Log.Logger.Debug($"SessionActor [{SessionID}] sent message [{scopedMessageName}]");
        }
        
        public void Close()
        {
            _socket.Close();
            _cts.Cancel();
            Context.Stop(Self);
        }
        
        public void FullInitialize(long ping)
        {
            // Ask the ActorFactory for this actor's message services.
            var services = _actorFactoryRef
                .Ask<HashSet<Type>>(ServiceFactory.LOADED_SERVICES_ASK)
                .Result;

            SetServices(services);

            Log.Logger.Information($"Session created with ID [{SessionID}] PING: [{ping}]");
        }

        protected override void PreStart()
        {
            // Ask the ActorFactory for this actor's message services.
            var services = _actorFactoryRef
                .Ask<HashSet<Type>>(ServiceFactory.UNLOADED_SERVICES_ASK)
                .Result;

            SetServices(services);

            Log.Logger.Debug($"SessionActor [{SessionID}] PreStart completed.");

            base.PreStart();
        }

        protected override void Unhandled(object message)
        {
            Log.Logger.Error($"CommunicationActor [{SessionID}] " +
                $"received unhandled message of type [{message.GetType()}].");
        }

        private void ConfigureReceivers()
        {
            Receive<INetworkMessage>(x => Send(x));
            Receive<string>(x => x == "Close", x => Close());
        }

        private void ListenAndProcess(IActorRef context)
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var buffer = new byte[BUFFER_SIZE];
                    var bytesReceived = _socket.Receive(buffer);
                    if (bytesReceived <= 0) continue;

                    var packet = GetPacketFromBuffer(buffer, bytesReceived);
                    if (packet == null) continue;

                    HandlePacket(packet);
                }
                catch (SocketException ex)
                {
                    Log.Logger.Error($"SessionActor [{SessionID}] socket error: {ex.Message}");
                    break;
                }
            }

            context.Tell("Close");
        }

        private void HandlePacket(INetworkMessage packet)
        {
            // Log the incoming packet.
            var scopedMessageName = packet
                .GetType()
                .ToString()
                .Split('.')[^1];
            Log.Logger.Debug($"SessionActor [{SessionID}] received message [{scopedMessageName}]");

            // Iterate our services and see if any of them can handle this message.
            foreach (var service in _services)
            {
                var actorRef = service.Key;
                var type = service.Value;

                if (type.MessageHandlers.Any(x => x.Key == packet.GetType()))
                {
                    actorRef.Tell(packet);
                    return;
                }
            }

            Log.Logger.Warning($"SessionActor [{SessionID}] INetworkMessage of type [{packet.GetType()}] was left unhandled.");
        }

        private INetworkMessage GetPacketFromBuffer(byte[] buffer, int bytesReceived)
        {
            var bufferSpan = new ReadOnlySpan<byte>(buffer, 0, bytesReceived).ToArray();
            if (!IsKIPacket(bufferSpan) || !TryDeserializePacket(bufferSpan, out var record))
            {
                Log.Logger.Error($"SessionActor [{SessionID}] received non-KINP packet.");
                return null;
            }

            return record;
        }

        private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            _isSending = false;
            if (e.SocketError != SocketError.Success)
            {
                Log.Logger.Error($"SessionActor [{SessionID}] send failure: {e.SocketError}");
                return;
            }
        }

        private void SetServices(HashSet<Type> services)
        {
            foreach (var service in services)
            {
                var props = Akka.Actor.Props.Create(service, this);
                var childRef = Context.ActorOf(props);

                // We've created the service as a child actor. Problem is, we need to know the actual class
                // identity to use it later. To do that, we'll ask the actor to identify itself.
                var identity = childRef.Ask<ServiceIdentityReply>(MessageService.ASK_IDENTIFY)
                    .Result
                    .Identity;
                _services.Add(childRef, identity);
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
