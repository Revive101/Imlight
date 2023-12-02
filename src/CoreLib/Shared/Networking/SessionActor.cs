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
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.Configuration;
using Imlight.Common.MessageLayer;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Services;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Networking;

/// <summary>
/// Represents a connected socket as a ReceiveActor.
/// </summary>
public class SessionActor : ReceiveActor, IDisposable {
    private readonly int _bufferSize                         = ConfigurationManager.Settings.SessionActorBufferSize;
    private readonly byte _asyncSendPoolCount                = ConfigurationManager.Settings.SessionActorSendPoolSize;
    private readonly byte _asyncReceivePoolCount             = ConfigurationManager.Settings.SessionActorReceivePoolSize;
    private readonly bool _closeOnSocketException            = ConfigurationManager.Settings.SessionActorCloseOnException;
    private readonly byte _serviceRetryCount                 = ConfigurationManager.Settings.SessionActorServiceRetryCount;
    private readonly byte _serviceTimeRangeRetryInSeconds    = ConfigurationManager.Settings.SessionActorServiceRangeRetry;
    private readonly int _tokenBucketMax                     = ConfigurationManager.Settings.SessionTokenBucketMax;
    private readonly int _tokenBucketPerSecond               = ConfigurationManager.Settings.SessionTokenBucketPerSecond;
    private readonly byte _tokenBucketFailedAcquisitionLimit = ConfigurationManager.Settings.SessionTokenBucketFailedAcquisitionLimit;

    public ushort SessionID                                  { get; }
    public uint OfferTime                                    { get; set; }
    public uint OfferMillisecondsIntoSecond                  { get; set; }
    public Socket Socket                                     { get; }
    public IActorRef ActorRef                                { get; }
    public IActorRef ServerRef                               { get; }
    public bool SessionValid                                 { get; private set; }
    public bool IsInQueue                                    { get; private set; }
    public ushort QueuePosition                              { get; private set; }
    public IMessage CachedDequeueMessage                     { get; set; }
    public long Ping                                         { get; private set; }
    public string Ip => Socket?.RemoteEndPoint?.ToString();
    public string RemoteIp => Socket?.RemoteEndPoint?.ToString().Split(':')[0];

    private readonly IActorRef _actorFactoryRef;
    private readonly Dictionary<IActorRef, MessageService> _services;
    private readonly SocketAsyncEventArgs _socketSendArgs = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _isSending;
    private bool _isDisposed;
    private List<IMessage> _preInitMessages;
    private readonly TokenBucket _tokenBucket;

    private readonly Stack<SocketAsyncEventArgs> _receiveEventArgPool = new();
    private readonly List<Type> _suppressedPackets;

    // ctor
    public SessionActor(Socket socket, ushort sessionId, IActorRef server) {
        this.Socket = socket;
        this.SessionID = sessionId;
        this._services = new Dictionary<IActorRef, MessageService>();
        this._preInitMessages = new List<IMessage>();
        this.ServerRef = server;
        this._suppressedPackets = new List<Type>
        {
            typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE),
            typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE),
            typeof(GAME_5_PROTOCOL.MSG_SERVERMOVE),
            typeof(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK),
            typeof(ControlMessageProtocol.KeepAlive),
            typeof(ControlMessageProtocol.KeepAliveResponse)
        };
        this._tokenBucket = new TokenBucket(_tokenBucketMax, _tokenBucketPerSecond);

        // To get the actor factory reference, we'll ask the server.
        var query = new SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY();
        this._actorFactoryRef = server.Ask<SERVER_100_PROTOCOL.MSG_ACTORFACTORYINFO>(query)
            .Result
            .Reference;

        ActorRef = Context.Self;

        ConfigureReceivers();
        ProcessReceive(GetReceiveEventArgsFromPool());
    }

    // Akka.NET ctor
    public static Props Props(Socket socket, ushort sessionId, IActorRef server)
        => Akka.Actor.Props.Create(() => new SessionActor(socket, sessionId, server));

    /// <summary>
    /// Places the session in the queue.
    /// </summary>
    /// <param name="pos"></param>
    public void PlaceInQueue(ushort pos) {
        IsInQueue = true;
        QueuePosition = pos;
    }

    /// <summary>
    /// Removes the session from the queue.
    /// </summary>
    public void Dequeue() {
        // Send the dequeue message to the socket.
        SendToSocket(CachedDequeueMessage);
    }

    /// <summary>
    /// Enqueues the session to the server.
    /// </summary>
    /// <returns></returns>
    public SERVER_100_PROTOCOL.MSG_PLAYERENQUEUEDRSP EnqueueToServer() {
        var msg = new SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED() {
            SessionActor = this
        };

        var rsp = ServerRef.Ask<SERVER_100_PROTOCOL.MSG_PLAYERENQUEUEDRSP>(msg)
            .Result;

        return rsp;
    }

    /// <summary>
    /// Enqueues the session to the server.
    /// </summary>
    /// <param name="serverRef"></param>
    /// <returns></returns>
    public IMessage EnqueueToServer(IActorRef serverRef) {
        var msg = new SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED() {
            SessionActor = this
        };

        var rsp = serverRef.Ask<IMessage>(msg)
            .Result;

        return rsp;
    }

    /// <summary>
    /// Dispatches a <see cref="IServerMessage"/> to any service that can handle the message.
    /// </summary>
    /// <param name="msg"></param>
    private void HandleInternalTell(IServerMessage msg) {
        // Iterate through services and forward the message to any service that can handle the message.
        var wasDispatched = false;
        foreach (var (actorRef, type) in _services) {
            if (type.MessageHandlers.All(x => x.Key != msg.GetType())) {
                continue;
            }

            actorRef.Forward(msg);
            wasDispatched = true;
        }

        if (!wasDispatched) {
            Unhandled(msg);
        }
    }

    /// <summary>
    /// Dispatches a <see cref="IServerMessage"/> to any service that can handle the message. Awaits a response
    /// with a timeout of 2 seconds.
    /// </summary>
    /// <param name="msg"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T HandleInternalAsk<T>(IServerMessage msg)
        where T : IServerMessage {
        // Iterate our services and see if any of them can handle this message.
        foreach (var (actorRef, type) in _services) {
            if (type.MessageHandlers.All(x => x.Key != msg.GetType())) {
                continue;
            }

            try {
                var result = actorRef.Ask<T>(msg, timeout: TimeSpan.FromSeconds(20)).Result;
                return result;
            }
            catch (Exception ex) {
                Logger.Error("SessionActor service attempted to ask another service with {0}, but the timeout " +
                          "was exceeded. {1}", Logger.Args(msg.GetType(), ex.Message));
            }
        }

        Unhandled(msg);
        return default(T);
    }

    /// <summary>
    /// Sends a message to the server and awaits a response.
    /// </summary>
    /// <param name="msg"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="SessionFatalException"></exception>
    public T AskServer<T>(IServerMessage msg)
        where T : IServerMessage {
        if (ServerRef is not null) {
            return ServerRef.Ask<T>(msg).Result;
        }

        throw new SessionFatalException($"SessionActor [{SessionID}] contained a null server reference!");
    }

    /// <summary>
    /// Gets the actor reference for the zone.
    /// </summary>
    /// <returns>The actor reference for the zone, or null if the zone service is not available.</returns>
    public IActorRef GetZoneActor() {
        // Check to see if we have a ZoneService.
        var zoneService = _services.FirstOrDefault(x => x.Value is ZoneService);
        if (zoneService.Key is null) {
            return null;
        }

        return ((ZoneService)zoneService.Value).ZoneActor;
    }

    /// <summary>
    /// Retrieves the associated account for the session actor.
    /// </summary>
    /// <returns>The associated account, or null if no account is found.</returns>
    public Account GetAssociatedAccount() {
        // Check to see if we have a LoginService.
        var accountService = _services.FirstOrDefault(x => x.Value is AccountService);
        if (accountService.Key is null) {
            return null;
        }

        return ((AccountService) accountService.Value).Account;
    }

    /// <summary>
    /// Disposes of the SessionActor.
    /// </summary>
    public void Dispose() {
        // Avoid duplicate Dispose calls.
        if (_isDisposed) {
            return;
        }

        _isDisposed = true;

        // Send a message to the server to deallocate this SessionActor.
        var msg = new SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET() {
            Id = SessionID,
            Socket = this.Socket,
            Ip = this.RemoteIp
        };
        ServerRef.Tell(msg);

        // Dispose services.
        SendPreDisposeToServices();
        SendDisposeToServices();

        // Dispose self.
        Context.Stop(Self);
        Socket?.Close();
        _cts.Cancel();

        _socketSendArgs?.Dispose();
        _cts?.Dispose();
        Socket?.Dispose();
    }

    protected override SupervisorStrategy SupervisorStrategy() {
        // Recall that child actors of the SessionActor are the message services.
        return new AllForOneStrategy(
            maxNrOfRetries: _serviceRetryCount,
            withinTimeRange: TimeSpan.FromSeconds(_serviceTimeRangeRetryInSeconds),
            localOnlyDecider: ex => {
                switch (ex) {
                    case ServiceRetryException tex: {
                            Logger.Error("SessionActor {Sid} service {Class} L:{LineNumber} threw restart exception: " +
                                      "{Message}", Logger.Args(SessionID, tex.CallingClass, tex.LineNumber, tex.Message));
                            return Directive.Restart;
                        }
                    case SessionFatalException tex: {
                            Logger.Error("SessionActor {Sid} service {Class} L:{LineNumber} threw fatal exception: " +
                                      "{Message}", Logger.Args(SessionID, tex.CallingClass, tex.LineNumber, tex.Message));
                            return Directive.Stop;
                        }
                    default:
                        return Directive.Stop;
                }
            }
        );
    }

    protected override void PreStart() {
        // Ask the ActorFactory for this actor's message services.
        var msg = new SERVICE_101_PROTOCOL.MSG_QUERYUNLOADEDSERVICES();
        var services = _actorFactoryRef
            .Ask<SERVICE_101_PROTOCOL.MSG_SERVICESLIST>(msg)
            .Result
            .Services;

        SetServices(services);

        base.PreStart();
    }

    protected override void Unhandled(object message) {
        // Bump this up to warning on release builds.
        Logger.Verbose("SessionActor {Id} received unhandled message of type {Type}.",
            Logger.Args(SessionID, message.GetType()));
    }

    private void ConfigureReceivers() {
        // Specific message handlers.
        Receive<string>(x => x == "Close", x => Dispose());
        Receive<string>(x => x == "Identify", x => Sender.Tell(this));
        Receive<SERVICE_101_PROTOCOL.MSG_GETALLSERVICES>(InitializeActiveSession);
        Receive<SERVER_100_PROTOCOL.MSG_PING>(x => this.Ping = x.Ping);
        Receive<Exception>(ReceiveException);

        // Generic message handlers.
        Receive<IServerMessage>(HandleInternalTell);
        Receive<IMessage>(SendToSocket);
    }

    private void InitializeActiveSession(SERVICE_101_PROTOCOL.MSG_GETALLSERVICES message) {
        // Ask the ActorFactory for this actor's message services.
        var msg = new SERVICE_101_PROTOCOL.MSG_QUERYLOADEDSERVICES();
        var services = _actorFactoryRef
            .Ask<SERVICE_101_PROTOCOL.MSG_SERVICESLIST>(msg)
            .Result
            .Services;

        SetServices(services);
        SessionValid = true;

        // Finally handle cached messages.
        if (_preInitMessages is null) {
            return;
        }

        foreach (var preInitMessage in _preInitMessages) {
            HandlePacket(preInitMessage);
        }
        _preInitMessages = null;
    }

    private void SetServices(List<Type> services) {
        foreach (var service in services) {
            var serviceName = $"{service}";
            var props = Akka.Actor.Props.Create(service, this);
            var childRef = Context.ActorOf(props, serviceName);

            Logger.Verbose("New actor created for session {Id}: {Name}",
                Logger.Args(SessionID, serviceName));

            // We've created the service as a child actor. Problem is, we need to know the actual class
            // identity to use it later. To do that, we'll ask the actor to identify itself.
            var msg = new SERVICE_101_PROTOCOL.MSG_QUERYMESSAGESERVICEIDENTITY();
            var identity = childRef.Ask<SERVICE_101_PROTOCOL.MSG_MESSAGESERVICEIDENTITY>(msg)
                .Result
                .Service;
            _services.Add(childRef, identity);
        }
    }

    private void ReceiveException(Exception ex) {
        Dispose();
        throw ex;
    }

    private void SendOldContextException(Exception ex) {
        ActorRef.Tell(ex);
    }

    private void SendPreDisposeToServices() {
        // Iterate through each service and send them a pre-dispose message. This lets a service gracefully handle
        // the dispose in the case that it requires another service to still be active.
        foreach (var (actorRef, type) in _services) {
            // If the service doesn't have a pre-dispose message handler, we'll just skip it.
            if (!type.MessageHandlers.ContainsKey(typeof(SERVICE_101_PROTOCOL.MSG_PREDISPOSE))) {
                continue;
            }

            // Await a reply. This is a blocking call to ensure that the service gracefully disposes.
            try {
                actorRef.Ask(new SERVICE_101_PROTOCOL.MSG_PREDISPOSE(), timeout: TimeSpan.FromSeconds(2)).Wait();
            }
            catch {
                continue;
            }
        }
    }

    private void SendDisposeToServices() {
        // Iterate through our services and send them a dispose message.
        foreach (var (actorRef, type) in _services) {
            actorRef.Tell(new SERVICE_101_PROTOCOL.MSG_DISPOSE());
        }
    }

    #region Socket Operations

    private void ProcessReceive(SocketAsyncEventArgs eventArgs) {
        if (!Socket.ReceiveAsync(eventArgs)) {
            OnReceiveCompleted(eventArgs);
        }
    }

    private void OnReceiveCompleted(SocketAsyncEventArgs e) {
        if (!_tokenBucket.TryAcquire()) {
            // The rate limit was reached.
            var failedAcquisitionCount = _tokenBucket.GetFailedAcquisitionCount();
            if (failedAcquisitionCount >= _tokenBucketFailedAcquisitionLimit) {
                // Log warning.
                Logger.Warning("SessionActor [{SessionId}] failed to acquire token {FailedAcquisitionCount} times.",
                    Logger.Args(SessionID, failedAcquisitionCount));

                // The session has exceeded the failed acquisition limit. We'll dispose of the session.
                SendOldContextException(new SessionFatalException($"SessionActor [{SessionID}] failed to acquire " +
                                                                  $"token {failedAcquisitionCount} times. " +
                                                                  $"Session will be disposed."));
                return;
            }

            return;
        }

        // If receive failed, chances are the socket suddenly disconnected.
        if (e.SocketError != SocketError.Success) {
            // If the socket is not connected, we'll just dispose of the session. We cannot just throw the error
            // here, because the actor context is on a different thread. We'll just send a message to the actor
            // and let it handle the error.
            SendOldContextException(new SessionFatalException($"SessionActor socket {e.SocketError}."));
            return;
        }
        if (e.BytesTransferred <= 0) {
            return;
        }

        var packet = GetPacketsFromBuffer(e.Buffer, e.BytesTransferred);
        if (packet != null && (SessionValid || packet[0].ServiceId == 0)) {
            foreach (var message in packet) {
                HandlePacket(message);
            }
        }
        else if (packet != null && !SessionValid) {
            // If the session still isn't created, cache all non-control messages for later processing.
            foreach (var message in packet) {
                _preInitMessages.Add(message);
            }
        }

        // Reset the buffer before putting it back into the pool.
        e.SetBuffer(null, 0, 0);
        _receiveEventArgPool.Push(e);

        var newArgs = new SocketAsyncEventArgs();
        newArgs.Completed += (_, e) => OnReceiveCompleted(e);
        newArgs.SetBuffer(new byte[_bufferSize], 0, _bufferSize);
        newArgs.AcceptSocket = this.Socket;
        ProcessReceive(newArgs);
    }

    private void SendToSocket(IMessage message) {
        if (!Socket.Connected) {
            SendOldContextException(new SessionFatalException(
                $"SessionActor [{SessionID}] cannot send message [{message.GetType()}] " +
                $"send failure: Socket is not connected!"));
        }
        if (_isSending) {
            Logger.Error("SessionActor {SessionId} send failure: " +
                      "Asynchronous send operation already in progress.", Logger.Args(SessionID));
            return;
        }

        var data = MessageSerializer.Encode(message);
        _isSending = true;
        _socketSendArgs.SetBuffer(data, 0, data.Length);
        _socketSendArgs.UserToken = Socket;

        var willRaiseEvent = Socket.SendAsync(_socketSendArgs);
        if (!willRaiseEvent) {
            OnSendCompleted(_socketSendArgs);
        }

        var scopedMessageName = message
            .GetType()
            .ToString()
            .Split('.')[^1]
            .Replace('+', '.');
        if (!_suppressedPackets.Contains(message.GetType())) {
            Logger.Verbose("SessionActor {Id} sent message {MessageName}",
                Logger.Args(SessionID, scopedMessageName));
        }
    }

    private void OnSendCompleted(SocketAsyncEventArgs e) {
        _isSending = false;
        if (e.SocketError != SocketError.Success) {
            SendOldContextException(
                new SessionFatalException($"SessionActor [{SessionID}] send failure: {e.SocketError}"));
        }
    }

    private void HandlePacket(IMessage packet) {
        // Logger the incoming packet.
        var scopedMessageName = packet
            .GetType()
            .ToString()
            .Split('.')[^1]
            .Replace('+', '.');
        if (!_suppressedPackets.Contains(packet.GetType())) {
            Logger.Verbose("SessionActor {SessionId} received KiNP packet {ScopedMessageName}",
                Logger.Args(SessionID, scopedMessageName));
        }

        // Iterate through services and forward the message to any service that can handle the message.
        var wasDispatched = false;
        foreach (var (actorRef, type) in _services) {
            if (type.MessageHandlers.All(x => x.Key != packet.GetType())) {
                continue;
            }

            actorRef.Forward(packet);
            wasDispatched = true;
        }

        if (!wasDispatched) {
            Unhandled(packet);
        }
    }

    private IMessage[] GetPacketsFromBuffer(byte[] buffer, int bytesReceived) {
        var bufferSpan = new ReadOnlySpan<byte>(buffer, 0, bytesReceived).ToArray();
        if (!IsKIPacket(bufferSpan)) {
            Logger.Debug("SessionActor {SessionId} received non-KINP packet", Logger.Args(SessionID));
            return null;
        }

        if (TryDeserializePacket(bufferSpan, out var records)) {
            return records.ToArray();
        }

        // The packet failed to deserialize.
        return null;

    }

    private bool TryDeserializePacket(byte[] buffer, out IReadOnlyCollection<IMessage> messages) {
        try {
            messages = MessageSerializer.Decode(buffer);
            return true;
        }
        catch (Exception ex) {
            Logger.Error("SessionActor {SessionID} packet deserialize failed: {ExMessage}",
                Logger.Args(SessionID, ex.InnerException?.Message ?? ex.Message));

            messages = null;
            return false;
        }
    }

    private bool IsKIPacket(byte[] buffer)
        => buffer.AsSpan()[..2].SequenceEqual(stackalloc byte[2] { 0x0D, 0xF0 });

    private SocketAsyncEventArgs GetReceiveEventArgsFromPool() {
        lock (_receiveEventArgPool) {
            if (_receiveEventArgPool.Count > 0) {
                return _receiveEventArgPool.Pop();
            }
            else if (_receiveEventArgPool.Count < 5) {
                // Create a new SocketAsyncEventArgs if the pool is empty and the pool limit has not been reached.
                var receiveEventArgs = new SocketAsyncEventArgs();
                receiveEventArgs.Completed += (_, e) => OnReceiveCompleted(e);
                receiveEventArgs.AcceptSocket = Socket;
                receiveEventArgs.SetBuffer(new byte[_bufferSize], 0, _bufferSize);

                return receiveEventArgs;
            }
        }

        SendOldContextException(new SessionFatalException($"SessionActor [{SessionID}] receive argument " +
                                                          $"pool over maximum allowed count " +
                                                          $"of {_asyncReceivePoolCount}."));
        return null;
    }

    #endregion
}
