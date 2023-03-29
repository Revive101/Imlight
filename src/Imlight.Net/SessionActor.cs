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
using System.Web;
using WizUnraveler;
using WizUnraveler.Cache;
using WizUnraveler.DML;

namespace Imlight.Net
{
    /// <summary>
    /// Represents a connected socket as a ReceiveActor.
    /// </summary>
    public class SessionActor : ReceiveActor, IDisposable
    {
        private const int  BUFFER_SIZE = 4096;
        private const byte ASYNC_SEND_POOL_COUNT = 3;
        private const byte ASYNC_RECEIVE_POOL_COUNT = 3;
        private const bool CLOSE_ON_SOCKET_EXCEPTION = true;

        public ushort SessionID                     { get; }
        public Socket Socket                        { get; }
        public IActorRef ActorRef                   { get; }
        public IActorRef ServerRef                  { get; }
        public bool SessionValid                    { get; private set; }
        public bool IsInQueue                       { get; private set; }
        public ushort QueuePosition                 { get; private set; }
        public INetworkMessage CachedDequeueMessage { get; set; }
        public long Ping                            { get; private set; }
        
        private readonly IActorRef _actorFactoryRef;
        private readonly Dictionary<IActorRef, MessageService> _services;
        private readonly SocketAsyncEventArgs _socketSendArgs = new SocketAsyncEventArgs();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isSending;
        private bool _isDisposed;
        private List<INetworkMessage> _preInitMessages;

        private readonly Stack<SocketAsyncEventArgs> _receiveEventArgPool = new();

        public SessionActor(Socket socket, ushort sessionId, IActorRef server)
        {
            this.Socket = socket;
            this.SessionID = sessionId;
            this._services = new Dictionary<IActorRef, MessageService>();
            this._preInitMessages = new List<INetworkMessage>();
            this.ServerRef = server;

            // To get the actor factory reference, we'll ask the server.
            var query = new SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY();
            this._actorFactoryRef = server.Ask<SERVER_100_PROTOCOL.MSG_ACTORFACTORYINFO>(query)
                .Result
                .Reference;

            ActorRef = Context.Self;

            ConfigureReceivers();
            ProcessReceive(GetReceiveEventArgsFromPool());
        }

        public static Props Props(Socket socket, ushort sessionId, IActorRef server)
        {
            return Akka.Actor.Props.Create(() => new SessionActor(socket, sessionId, server));
        }

        public void PlaceInQueue(ushort pos)
        {
            IsInQueue = true;
            QueuePosition = pos;
        }

        public void Dequeue()
        {
            // Send the dequeue message to the socket.
            SendToSocket(CachedDequeueMessage);
        }

        public INetworkMessage EnqueueToServer()
        {
            var msg = new SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED()
            {
                SessionActor = this
            };

            var rsp = ServerRef.Ask<INetworkMessage>(msg)
                .Result;

            return rsp;
        }
        
        public INetworkMessage EnqueueToServer(IActorRef serverRef)
        {
            var msg = new SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED()
            {
                SessionActor = this
            };

            var rsp = serverRef.Ask<INetworkMessage>(msg)
                .Result;

            return rsp;
        }

        public T HandleInternalAsk<T>(IServerMessage msg) 
            where T : IServerMessage
        {
            // Iterate our services and see if any of them can handle this message.
            foreach (var service in _services)
            {
                var actorRef = service.Key;
                var type = service.Value;

                if (type.MessageHandlers.Any(x => x.Key == msg.GetType()))
                {
                    //Sender.Forward(actorRef.Ask<T>(msg));
                    var result = actorRef.Ask<T>(msg).Result;
                    return result;
                }
            }

            Unhandled(msg);
            return default(T);
        }

        public T AskServer<T>(IServerMessage msg)
            where T : IServerMessage
        {
            if (ServerRef is null)
            {
                Log.Logger.Fatal($"SessionActor [{SessionID}] contained a null server reference!");
                return default;
            }
            
            return ServerRef.Ask<T>(msg).Result;
        }
        
        public void Dispose()
        {
            // Avoid duplicate Dispose calls.
            if (_isDisposed) return;
            _isDisposed = true;

            // Send a message to the server to deallocate this SessionActor.
            var msg = new SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET()
            {
                Id = SessionID,
                Socket = this.Socket,
                Ip = this.Socket?.RemoteEndPoint?.ToString()
            };
            ServerRef.Tell(msg);
            
            // Iterate through our services and send them a dispose message.
            foreach (var service in _services)
            {
                var actorRef = service.Key;
                var type = service.Value;

                if (type.MessageHandlers.Any(x => x.Key == typeof(SERVICE_101_PROTOCOL.MSG_DISPOSE)))
                {
                    actorRef.Tell(new SERVICE_101_PROTOCOL.MSG_DISPOSE());
                }
            }
            
            Context.Stop(Self);
            Socket?.Close();
            _cts.Cancel();
            
            _socketSendArgs?.Dispose();
            _cts?.Dispose();
            Socket?.Dispose();
        }

        protected override void PreStart()
        {
            // Ask the ActorFactory for this actor's message services.
            var msg = new SERVICE_101_PROTOCOL.MSG_QUERYUNLOADEDSERVICES();
            var services = _actorFactoryRef
                .Ask<SERVICE_101_PROTOCOL.MSG_SERVICESLIST>(msg)
                .Result
                .Services;

            SetServices(services);

            Log.Logger.Debug($"SessionActor [{SessionID}] PreStart completed.");

            base.PreStart();
        }

        protected override void Unhandled(object message)
        {
            Log.Logger.Warning($"SessionActor [{SessionID}] " +
                $"received unhandled message of type [{message.GetType()}].");
        }

        private void ConfigureReceivers()
        {
            // Specific message handlers.
            Receive<SERVICE_101_PROTOCOL.MSG_GETALLSERVICES>(InitializeActiveSession);
            Receive<SERVER_100_PROTOCOL.MSG_PING>(x => this.Ping = x.Ping);

            // Generic message handlers.
            Receive<IServerMessage>(HandleInternalTell);
            Receive<INetworkMessage>(SendToSocket);
            Receive<string>(x => x == "Close", x => Dispose());
            Receive<string>(x => x == "Identify", x => Sender.Tell(this));
        }

        private void HandleInternalTell(IServerMessage msg)
        {
            // Iterate through services and forward the message to any service that can handle the message.
            var wasDispatched = false;
            foreach (var service in _services)
            {
                var actorRef = service.Key;
                var type = service.Value;

                if (!type.MessageHandlers.Any(x => x.Key == msg.GetType())) continue;

                actorRef.Forward(msg);
                wasDispatched = true;
            }

            if (!wasDispatched)
                Unhandled(msg);
        }

        private void InitializeActiveSession(SERVICE_101_PROTOCOL.MSG_GETALLSERVICES message)
        {
            // Ask the ActorFactory for this actor's message services.
            var msg = new SERVICE_101_PROTOCOL.MSG_QUERYLOADEDSERVICES();
            var services = _actorFactoryRef
                .Ask<SERVICE_101_PROTOCOL.MSG_SERVICESLIST>(msg)
                .Result
                .Services;

            SetServices(services);
            SessionValid = true;

            // Finally handle cached messages.
            if (_preInitMessages is null) return;
            foreach (var preInitMessage in _preInitMessages)
            {
                HandlePacket(preInitMessage);
            }
            _preInitMessages = null;
        }

        private void SetServices(List<Type> services)
        {
            foreach (var service in services)
            {
                var serviceName = $"{service}";
                var props = Akka.Actor.Props.Create(service, this);
                var childRef = Context.ActorOf(props, serviceName);

                Log.Logger.Debug($"New actor created under {Context.Self.Path}: {serviceName}");

                // We've created the service as a child actor. Problem is, we need to know the actual class
                // identity to use it later. To do that, we'll ask the actor to identify itself.
                var msg = new SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY();
                var identity = childRef.Ask<SERVICE_101_PROTOCOL.MSG_MESSAGESERVICEIDENTITY>(msg)
                    .Result
                    .Service;
                _services.Add(childRef, identity);
            }
        }

        #region Socket Operations

        private void ProcessReceive(SocketAsyncEventArgs eventArgs)
        {
            if (!Socket.ReceiveAsync(eventArgs))
                OnReceiveCompleted(eventArgs);
        }

        private void OnReceiveCompleted(SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                Log.Logger.Error($"SessionActor receive error: {e.SocketError}");
                return;
            }
            else if (e.BytesTransferred <= 0) return;

            var packet = GetPacketFromBuffer(e.Buffer, e.BytesTransferred);

            if (packet != null && (SessionValid || packet.ServiceID == 0))
            {
                HandlePacket(packet);
            }
            else if (packet != null && !SessionValid)
            {
                // If the session still isn't created, cache all non-control messages for later processing.
                _preInitMessages.Add(packet);
            }

            // Reset the buffer before putting it back into the pool.
            e.SetBuffer(null, 0, 0);
            _receiveEventArgPool.Push(e);

            var newArgs = new SocketAsyncEventArgs();
            newArgs.Completed += (_, e) => OnReceiveCompleted(e);
            newArgs.SetBuffer(new byte[BUFFER_SIZE], 0, BUFFER_SIZE);
            newArgs.AcceptSocket = this.Socket;
            ProcessReceive(newArgs);
        }

        private void SendToSocket(INetworkMessage message)
        {
            if (!Socket.Connected)
            {
                Log.Logger.Error($"SessionActor [{SessionID}] cannot send message [{message.GetType()}] " +
                                 $"send failure: " +
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
            _socketSendArgs.SetBuffer(data, 0, data.Length);
            _socketSendArgs.UserToken = Socket;

            var willRaiseEvent = Socket.SendAsync(_socketSendArgs);
            if (!willRaiseEvent) OnSendCompleted(_socketSendArgs);

            var scopedMessageName = message
                .GetType()
                .ToString()
                .Split('.')[^1]
                .Replace('+', '.');
            Log.Logger.Debug($"SessionActor [{SessionID}] sent message [{scopedMessageName}]");
        }

        private void OnSendCompleted(SocketAsyncEventArgs e)
        {
            _isSending = false;
            if (e.SocketError != SocketError.Success)
            {
                Log.Logger.Error($"SessionActor [{SessionID}] send failure: {e.SocketError}");
                return;
            }
        }

        private void HandlePacket(INetworkMessage packet)
        {
            // Log the incoming packet.
            var scopedMessageName = packet
                .GetType()
                .ToString()
                .Split('.')[^1]
                .Replace('+', '.');
            Log.Logger.Verbose($"SessionActor [{SessionID}] received KiNP packet [{scopedMessageName}]");

            // Iterate through services and forward the message to any service that can handle the message.
            var wasDispatched = false;
            foreach (var service in _services)
            {
                var actorRef = service.Key;
                var type = service.Value;

                if (!type.MessageHandlers.Any(x => x.Key == packet.GetType())) continue;

                actorRef.Forward(packet);
                wasDispatched = true;
            }

            if (!wasDispatched) Unhandled(packet);
        }

        private INetworkMessage GetPacketFromBuffer(byte[] buffer, int bytesReceived)
        {
            var bufferSpan = new ReadOnlySpan<byte>(buffer, 0, bytesReceived).ToArray();
            if (!IsKIPacket(bufferSpan))
            {
                Log.Logger.Error($"SessionActor [{SessionID}] received non-KINP packet.");
                return null;
            }
            if (!TryDeserializePacket(bufferSpan, out var record))
            {
                Log.Logger.Error($"SessionActor [{SessionID}] packet failed to deserialize.");
                return null;
            }

            return record;
        }

        private bool TryDeserializePacket(byte[] buffer, out INetworkMessage message)
        {
            try
            {
                message = MessageSerializer.DeserializeMessageBinary(buffer);
                return true;
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"SessionActor [{SessionID}] packet deserialize failed: {ex.Message}");

                message = null;
                return false;
            }
        }

        private bool IsKIPacket(byte[] buffer)
            => (buffer.AsSpan()[0..2].SequenceEqual(stackalloc byte[2] { 0x0D, 0xF0 }));

        private SocketAsyncEventArgs GetReceiveEventArgsFromPool()
        {
            lock (_receiveEventArgPool)
            {
                if (_receiveEventArgPool.Count > 0)
                {
                    return _receiveEventArgPool.Pop();
                }
                else if (_receiveEventArgPool.Count < 5)
                {
                    // Create a new SocketAsyncEventArgs if the pool is empty and the pool limit has not been reached.
                    var receiveEventArgs = new SocketAsyncEventArgs();
                    receiveEventArgs.Completed += (_, e) => OnReceiveCompleted(e);
                    receiveEventArgs.AcceptSocket = Socket;
                    receiveEventArgs.SetBuffer(new byte[BUFFER_SIZE], 0, BUFFER_SIZE);

                    return receiveEventArgs;
                }
            }

            throw new InvalidOperationException($"SessionActor [{SessionID}] receive argument " +
                $"pool over maximum allowed count of {ASYNC_RECEIVE_POOL_COUNT}.");
        }

        #endregion
    }
}
