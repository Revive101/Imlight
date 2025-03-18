/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imlight.Common;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Services;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Shared.Networking;

/// <summary>
/// Represents a connected socket as a ReceiveActor.
/// </summary>
public sealed class SessionActor : ReceiveActor, IDisposable {

    private readonly byte _serviceRetryCount                 = ConfigurationManager.Settings["SessionActorServiceRetryCount"].AsByte();
    private readonly byte _serviceTimeRangeRetryInSeconds    = ConfigurationManager.Settings["SessionActorServiceRangeRetry"].AsByte();

    public ushort SessionID                                  { get; }
    public uint OfferTime                                    { get; set; }
    public uint OfferMillisecondsIntoSecond                  { get; set; }
    public IActorRef ActorRef                                { get; }
    public IActorRef ServerRef                               { get; }
    public bool SessionValid                                 { get; private set; }
    public bool IsInQueue                                    { get; private set; }
    public ushort QueuePosition                              { get; private set; }
    public IMessage CachedDequeueMessage                     { get; set; }
    public long Ping                                         { get; private set; }

    public string Ip;
    public string RemoteIp;

    private readonly IActorRef _actorFactoryRef;
    private readonly Dictionary<IActorRef, MessageService> _services;
    private readonly Socket _socket;
    private readonly List<IMessage> _preInitMessages = new();
    private IActorRef _socketListenerRef;
    private IActorRef _socketSenderRef;
    private bool _isDisposed;

    // ctor
    public SessionActor(Socket socket, ushort sessionId, IActorRef server) {
        this._socket = socket;
        this.Ip = socket.RemoteEndPoint.ToString();
        this.RemoteIp = socket.RemoteEndPoint.ToString().Split(':')[0];
        this.SessionID = sessionId;
        this._services = new Dictionary<IActorRef, MessageService>();
        this.ServerRef = server;

        // To get the actor factory reference, we'll ask the server.
        var query = new SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY();
        this._actorFactoryRef = server.Ask<SERVER_100_PROTOCOL.MSG_ACTORFACTORYINFO>(query)
            .Result
            .Reference;

        ActorRef = Context.Self;

        CreateSocketActors(socket);
        ConfigureReceivers();
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
        _socketListenerRef.Tell(CachedDequeueMessage);
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

            // If the handler is the sender, skip.
            if (actorRef == Sender) {
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

            // If the handler is the sender, skip.
            if (actorRef == Sender) {
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

        Logger.Debug("SessionActor {Id} disposing.", Logger.Args(SessionID));
        _isDisposed = true;

        // Send a message to the server to deallocate this SessionActor.
        var msg = new SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET() {
            Id = SessionID,
            Socket = this._socket,
            Ip = this.RemoteIp
        };
        ServerRef.Tell(msg);

        _socketListenerRef.Tell("Close");

        // Dispose services.
        SendPreDisposeToServices();
        SendDisposeToServices();

        Sender.Tell("DoneDisposing");

        // Dispose self.
        ActorRef.Tell(PoisonPill.Instance);
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        // Recall that child actors of the SessionActor are the message services.
        new AllForOneStrategy(
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
        Receive<SERVER_100_PROTOCOL.MSG_RECEIVEDPACKET>(x => HandlePacket(x.Packet));

        // Generic message handlers.
        Receive<IServerMessage>(HandleInternalTell);
        Receive<IMessage>(SendToSocket);
    }

    private void CreateSocketActors(Socket socket) {
        // Create the socket receiver actor.
        var props = Akka.Actor.Props.Create(() => new SocketListener(Self, socket, SessionID));
        _socketListenerRef = Context.ActorOf(props, $"SocketListener-{SessionID}");

        // Create the socket sender actor.
        var senderProps = Akka.Actor.Props.Create(() => new SocketSender(Self, socket, SessionID));
        _socketSenderRef = Context.ActorOf(senderProps, $"SocketSender-{SessionID}");
    }

    private void SendToSocket(IMessage message) {
        _socketSenderRef.Forward(message);
    }

    private void InitializeActiveSession(SERVICE_101_PROTOCOL.MSG_GETALLSERVICES message) {
        SessionValid = true;

        Logger.Debug("SessionActor {Id} initialized with all services.", Logger.Args(SessionID));

        foreach (var preInitMessage in _preInitMessages) {
            HandlePacket(preInitMessage);
        }
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
    }

    private void HandlePacket(IMessage packet) {
        // If the session still is not valid (the client hasn't completed the session handshake)
        // we'll cache all non-control messages for later processing.
        if (!SessionValid && packet.ServiceId != 0) {
            _preInitMessages.Add(packet);

            Logger.Verbose("SessionActor {Id} cached message {MessageName} for later processing.",
                Logger.Args(SessionID, packet.GetType().Name));
                
            return;
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
    
}
