/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using Akka.Actor;
using WizUnraveler;
using WizUnraveler.DML;
using WizUnraveler.Cache;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Shared.Networking
{
    /// <summary>
    /// Represents a connected socket as a ReceiveActor.
    /// </summary>
    public class SessionActor : ReceiveActor, IDisposable
    {
        private const int  BufferSize = 4096;
        private const byte AsyncSendPoolCount = 3;
        private const byte AsyncReceivePoolCount = 3;
        private const bool CloseOnSocketException = true;
        private const byte ServiceRetryCount = 3;
        private const byte ServiceTimeRangeRetryInSeconds = 30;

        public ushort SessionID                     { get; }
        public uint OfferTime                       { get; set; }
        public uint OfferMillisecondsIntoSecond     { get; set; }
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
        private readonly List<Type> _suppressedPackets;

        public SessionActor(Socket socket, ushort sessionId, IActorRef server)
        {
            this.Socket = socket;
            this.SessionID = sessionId;
            this._services = new Dictionary<IActorRef, MessageService>();
            this._preInitMessages = new List<INetworkMessage>();
            this.ServerRef = server;
            this._suppressedPackets = new List<Type>
            {
                typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE),
                typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE),
                typeof(GAME_5_PROTOCOL.MSG_SERVERMOVE)
            };

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
        
        private void HandleInternalTell(IServerMessage msg)
        {
            // Iterate through services and forward the message to any service that can handle the message.
            var wasDispatched = false;
            foreach (var (actorRef, type) in _services)
            {
                if (type.MessageHandlers.All(x => x.Key != msg.GetType())) 
                    continue;

                actorRef.Forward(msg);
                wasDispatched = true;
            }

            if (!wasDispatched)
                Unhandled(msg);
        }

        public T HandleInternalAsk<T>(IServerMessage msg) 
            where T : IServerMessage
        {
            // Iterate our services and see if any of them can handle this message.
            foreach (var (actorRef, type) in _services)
            {
                if (type.MessageHandlers.All(x => x.Key != msg.GetType())) 
                    continue;
                
                var result = actorRef.Ask<T>(msg).Result;
                return result;
            }

            Unhandled(msg);
            return default(T);
        }

        public T AskServer<T>(IServerMessage msg)
            where T : IServerMessage
        {
            if (ServerRef is not null) 
                return ServerRef.Ask<T>(msg).Result;

            throw new SessionFatalException($"SessionActor [{SessionID}] contained a null server reference!");
        }

        public void Dispose()
        {
            // Avoid duplicate Dispose calls.
            if (_isDisposed) 
                return;
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
            foreach (var (actorRef, type) in _services)
            {
                actorRef.Tell(new SERVICE_101_PROTOCOL.MSG_DISPOSE());
            }
            
            Context.Stop(Self);
            Socket?.Close();
            _cts.Cancel();
            
            _socketSendArgs?.Dispose();
            _cts?.Dispose();
            Socket?.Dispose();
        }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            // Recall that child actors of the SessionActor are the message services.
            return new AllForOneStrategy(
                maxNrOfRetries: ServiceRetryCount,
                withinTimeRange: TimeSpan.FromSeconds(ServiceTimeRangeRetryInSeconds),
                localOnlyDecider: ex =>
                {
                    switch (ex)
                    {
                        case ServiceRetryException tex:
                        {
                            Log.Error("SessionActor {Sid} service {Class} L:{LineNumber} threw restart exception: " +
                                             "{Message}", Log.Args(SessionID, tex.CallingClass, tex.LineNumber, tex.Message));
                            return Directive.Restart;
                        }
                        case SessionFatalException tex:
                        {
                            Log.Error("SessionActor {Sid} service {Class} L:{LineNumber} threw fatal exception: " +
                                       "{Message}", Log.Args(SessionID, tex.CallingClass, tex.LineNumber, tex.Message));
                            return Directive.Escalate;
                        }
                        default:
                            return Directive.Escalate;
                    }
                }
            );
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

            Log.Debug("SessionActor {Id} PreStart completed.", Log.Args(SessionID));

            base.PreStart();
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Log.Error("SessionActor {Id} restarting due to {ExceptionType}: {ExceptionMessage}",
                Log.Args(SessionID, reason.GetType(), reason.Message));
            
            base.PreRestart(reason, message);
        }

        protected override void Unhandled(object message)
        {
            // Bump this up to warning on release builds.
            Log.Verbose("SessionActor {Id} received unhandled message of type {Type}.", 
                Log.Args(SessionID, message.GetType()));
        }

        private void ConfigureReceivers()
        {
            // Specific message handlers.
            Receive<string>(x => x == "Close", x => Dispose());
            Receive<string>(x => x == "Identify", x => Sender.Tell(this));
            Receive<SERVICE_101_PROTOCOL.MSG_GETALLSERVICES>(InitializeActiveSession);
            Receive<SERVER_100_PROTOCOL.MSG_PING>(x => this.Ping = x.Ping);
            Receive<Exception>(ReceiveException);

            // Generic message handlers.
            Receive<IServerMessage>(HandleInternalTell);
            Receive<INetworkMessage>(SendToSocket);
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

                Log.Verbose("New actor created under {Path}: {Name}", 
                    Log.Args(Context.Self.Path, serviceName));

                // We've created the service as a child actor. Problem is, we need to know the actual class
                // identity to use it later. To do that, we'll ask the actor to identify itself.
                var msg = new SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY();
                var identity = childRef.Ask<SERVICE_101_PROTOCOL.MSG_MESSAGESERVICEIDENTITY>(msg)
                    .Result
                    .Service;
                _services.Add(childRef, identity);
            }
        }

        private void ReceiveException(Exception ex)
        {
            throw ex;
        }

        private void SendOldContextException(Exception ex)
        {
            ActorRef.Tell(ex);
        }

        #region Socket Operations

        private void ProcessReceive(SocketAsyncEventArgs eventArgs)
        {
            if (!Socket.ReceiveAsync(eventArgs))
                OnReceiveCompleted(eventArgs);
        }

        private void OnReceiveCompleted(SocketAsyncEventArgs e)
        {
            // If receive failed, chances are the socket suddenly disconnected.
            if (e.SocketError != SocketError.Success)
            {
                // If the socket is not connected, we'll just dispose of the session. We cannot just throw the error
                // here, because the actor context is on a different thread. We'll just send a message to the actor
                // and let it handle the error.
                SendOldContextException(new SessionFatalException($"SessionActor socket {e.SocketError}."));
                return;
            }
            if (e.BytesTransferred <= 0) 
                return;

            var packet = GetPacketFromBuffer(e.Buffer, e.BytesTransferred);
            if (packet != null && (SessionValid || packet.ServiceId == 0))
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
            newArgs.SetBuffer(new byte[BufferSize], 0, BufferSize);
            newArgs.AcceptSocket = this.Socket;
            ProcessReceive(newArgs);
        }

        private void SendToSocket(INetworkMessage message)
        {
            if (!Socket.Connected)
            {
                SendOldContextException(new SessionFatalException(
                    $"SessionActor [{SessionID}] cannot send message [{message.GetType()}] " +
                    $"send failure: Socket is not connected!"));
            }
            if (_isSending)
            {
                Log.Error("SessionActor {SessionId} send failure: " +
                                 "Asynchronous send operation already in progress.", Log.Args(SessionID));
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
            if (!_suppressedPackets.Contains(message.GetType()))
                Log.Verbose("SessionActor {Id} sent message {MessageName}", 
                    Log.Args(SessionID, scopedMessageName));
        }

        private void OnSendCompleted(SocketAsyncEventArgs e)
        {
            _isSending = false;
            if (e.SocketError != SocketError.Success)
            {
                SendOldContextException(
                    new SessionFatalException($"SessionActor [{SessionID}] send failure: {e.SocketError}"));
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
            if (!_suppressedPackets.Contains(packet.GetType()))
                Log.Verbose("SessionActor {SessionId} received KiNP packet {ScopedMessageName}",
                    Log.Args(SessionID, scopedMessageName));

            // Iterate through services and forward the message to any service that can handle the message.
            var wasDispatched = false;
            foreach (var (actorRef, type) in _services)
            {
                if (type.MessageHandlers.All(x => x.Key != packet.GetType())) continue;

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
                Log.Warning("SessionActor {SessionId} received non-KINP packet", Log.Args(SessionID));
                return null;
            }

            if (TryDeserializePacket(bufferSpan, out var record)) 
                return record;
            
            // The packet failed to deserialize.
            Log.Error("SessionActor {SessionId} packet failed to deserialize", Log.Args(SessionID));
            return null;

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
                Log.Error("SessionActor {SessionID} packet deserialize failed: {ExMessage}",
                    Log.Args(SessionID, ex.Message));

                message = null;
                return false;
            }
        }

        private bool IsKIPacket(byte[] buffer)
            => buffer.AsSpan()[..2].SequenceEqual(stackalloc byte[2] { 0x0D, 0xF0 });

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
                    receiveEventArgs.SetBuffer(new byte[BufferSize], 0, BufferSize);

                    return receiveEventArgs;
                }
            }
            
            SendOldContextException(new SessionFatalException($"SessionActor [{SessionID}] receive argument " +
                                                              $"pool over maximum allowed count " +
                                                              $"of {AsyncReceivePoolCount}."));
            return null;
        }

        #endregion
    }
}
