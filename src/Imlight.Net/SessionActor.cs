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
using WizUnraveler.DML;

namespace Imlight.Net
{
    /// <summary>
    /// Represents a connected socket as a ReceiveActor.
    /// </summary>
    public class SessionActor : ReceiveActor
    {
        private const int BUFFER_SIZE = 4096;

        public ushort SessionID                     { get; }
        public Socket Socket                        { get; }
        public IActorRef ActorRef                   { get; }
        public IActorRef ServerRef                  { get; }
        public bool SessionValid                    { get; private set; }
        public bool IsInQueue                       { get; private set; }
        public ushort QueuePosition                 { get; private set; }
        public INetworkMessage CachedDequeueMessage { get; set; }
        
        private readonly IActorRef _actorFactoryRef;
        private readonly Dictionary<IActorRef, MessageService> _services;
        private readonly SocketAsyncEventArgs _sendEventArgs = new SocketAsyncEventArgs();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isSending;
        private List<INetworkMessage> _preInitMessages;

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

            Task.Factory.StartNew(() => ListenAndProcess(ActorRef));
        }

        public static Props Props(Socket socket, ushort sessionId, IActorRef server)
        {
            return Akka.Actor.Props.Create(() => new SessionActor(socket, sessionId, server));
        }

        /// <summary>
        /// Send an INetworkMessage record to the connected socket.
        /// </summary>
        /// <param name="message"></param>
        public void Send(INetworkMessage message)
        {
            if (!Socket.Connected)
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
            _sendEventArgs.UserToken = Socket;
            _sendEventArgs.Completed += SendEventArgs_Completed;

            var willRaiseEvent = Socket.SendAsync(_sendEventArgs);
            if (!willRaiseEvent)
            {
                SendEventArgs_Completed(this, _sendEventArgs);
            }

            var scopedMessageName = message
                .GetType().ToString().Split('.')[^1];
            Log.Logger.Debug($"SessionActor [{SessionID}] sent message [{scopedMessageName}]");
        }
        
        /// <summary>
        /// Closes this active session and socket.
        /// </summary>
        public void Close()
        {
            Socket.Close();
            _cts.Cancel();
            
            // Send a message to the server to deallocate this SessionActor.
            var msg = new SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET()
            {
                ID = SessionID
            };
            ServerRef.Tell(msg);
            
            Context.Stop(Self);
        }
        
        /// <summary>
        /// Fully initializes this SessionActor by allocating its active services.
        /// </summary>
        public void InitializeActiveSession()
        {
            // Ask the ActorFactory for this actor's message services.
            var services = _actorFactoryRef
                .Ask<HashSet<Type>>(ServiceFactory.LOADED_SERVICES_ASK)
                .Result;

            SetServices(services);
            SessionValid = true;

            // Finally handle cached messages.
            foreach (var msg in _preInitMessages)
            {
                HandlePacket(msg);
            }
            _preInitMessages = null;
        }

        public void PlaceInQueue(ushort pos)
        {
            IsInQueue = true;
            QueuePosition = pos;
        }

        public void Dequeue()
        {
            // Send the dequeue message to the socket.
            Send(CachedDequeueMessage);
        }

        /// <summary>
        /// Asks the currently connected server if it can join it.
        /// </summary>
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
        
        /// <summary>
        /// Asks a server actor reference if it can join it.
        /// </summary>
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
        
        public void ForceToServer()
        {
            var msg = new SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED()
            {
                SessionActor = this,
                VIPEntry = true
            };
            
            ServerRef.Tell(msg);
        }

        public void ForceToServer(IActorRef serverRef)
        {
            var msg = new SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED()
            {
                SessionActor = this,
                VIPEntry = true
            };
            
            serverRef.Tell(msg);
        }

        protected override void PreStart()
        {
            InitializePreemptiveServices();

            Log.Logger.Debug($"SessionActor [{SessionID}] PreStart completed.");

            base.PreStart();
        }

        protected override void Unhandled(object message)
        {
            Log.Logger.Error($"SessionActor [{SessionID}] " +
                $"received unhandled message of type [{message.GetType()}].");
        }

        private void InitializePreemptiveServices()
        {
            // Ask the ActorFactory for this actor's message services.
            var services = _actorFactoryRef
                .Ask<HashSet<Type>>(ServiceFactory.UNLOADED_SERVICES_ASK)
                .Result;

            SetServices(services);
        }

        private void ConfigureReceivers()
        {
            Receive<INetworkMessage>(x => Send(x));
            Receive<string>(x => x == "Close", x => Close());
            Receive<string>(x => x == "Identify", x=> Sender.Tell(this));

            // Anything else is an internal message. Usually for one service to send a message
            // to another service.
            // @todo: deprecate this
            Receive<IInternalMessage>(x => HandleInternalTell(x));
        }

        private void ListenAndProcess(IActorRef context)
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var buffer = new byte[BUFFER_SIZE];
                    var bytesReceived = Socket.Receive(buffer);
                    if (bytesReceived <= 0) continue;

                    var packet = GetPacketFromBuffer(buffer, bytesReceived);
                    if (packet == null) continue;

                    // If the session has not been created yet, bank all non-control messages and process them
                    // after the session is valid.
                    if (!SessionValid && packet.ServiceID != 0)
                    {
                        _preInitMessages.Add(packet);
                        continue;
                    }

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
            Log.Logger.Verbose($"SessionActor [{SessionID}] received KiNP packet [{scopedMessageName}]");

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

            Unhandled(packet);
        }

        private void HandleInternalTell(IInternalMessage msg)
        {
            // Iterate our services and see if any of them can handle this message.
            foreach (var service in _services)
            {
                var actorRef = service.Key;
                var type = service.Value;

                if (type.MessageHandlers.Any(x => x.Key == msg.GetType()))
                {
                    actorRef.Forward(msg);
                    return;
                }
            }

            Unhandled(msg);
        }

        public T HandleInternalAsk<T>(IInternalMessage msg) 
            where T : IInternalMessage
        {
            // @fixme: Use actor mailbox to handle internal messages.

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

        public T AskServer<T>(INetworkMessage msg)
            where T : INetworkMessage
        {
            if (ServerRef is null)
            {
                Log.Logger.Fatal($"SessionActor [{SessionID}] contained a null server reference!");
                return default;
            }
            
            return ServerRef.Ask<T>(msg).Result;
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
                var serviceName = $"{service}.{RandomGen.GenerateGUID()}";
                var props = Akka.Actor.Props.Create(service, this);
                var childRef = Context.ActorOf(props, serviceName);

                // We've created the service as a child actor. Problem is, we need to know the actual class
                // identity to use it later. To do that, we'll ask the actor to identify itself.
                var identity = childRef.Ask<INTMSG_SERVICE_IDENTITY>(MessageService.ASK_IDENTIFY)
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
            catch (Exception ex)
            {
                Log.Logger.Error($"SessionActor [{SessionID}] packet deserialize failed: {ex.Message}");

                message = null;
                return false;
            }
        }

        private bool IsKIPacket(byte[] buffer)
            => (buffer.AsSpan()[0..2].SequenceEqual(stackalloc byte[2] { 0x0D, 0xF0 }));
    }
}
