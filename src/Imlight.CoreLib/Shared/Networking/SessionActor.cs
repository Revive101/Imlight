/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * SESSION ACTOR
 * ========================================================================
 * 
 * PURPOSE:
 * Manages a network session lifecycle, handling socket connections, 
 * message routing, and inter-service communication in a distributed 
 * actor-based networking system.
 * 
 * USAGE EXAMPLE:
 * // Session actor is typically created and managed by server infrastructure
 * // Handles message dispatching, service initialization, and session management
 * 
 * NOTE:
 * - Core component of distributed network communication
 * - Manages socket listeners, message services, and session state
 * - Supports dynamic service loading and message routing
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/09/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

    private readonly byte _serviceRetryCount                 = ConfigurationManager.Settings["Advanced.SessionActorServiceRetryCount"].AsByte();
    private readonly byte _serviceTimeRangeRetryInSeconds    = ConfigurationManager.Settings["Advanced.SessionActorServiceRangeRetry"].AsByte();

    private static readonly TimeSpan s_clientCloseTimeout    = TimeSpan.FromSeconds(10);

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
    private readonly Dictionary<Type, List<IActorRef>> _dispatchTable = [];
    private readonly Socket _socket;
    private readonly List<IMessage> _preInitMessages = new();
    private IActorRef _socketListenerRef;
    private IActorRef _socketSenderRef;
    private bool _isDisposed;
    private bool _stoppedByStrategy;
    private ICancelable _clientCloseTimer;

    // ctor
    public SessionActor(Socket socket, ushort sessionId, IActorRef server, IActorRef actorFactoryRef = null) {
        this._socket = socket;
        this.Ip = socket.RemoteEndPoint.ToString();
        this.RemoteIp = socket.RemoteEndPoint.ToString().Split(':')[0];
        this.SessionID = sessionId;
        this._services = new Dictionary<IActorRef, MessageService>();
        this.ServerRef = server;

        if (actorFactoryRef != null) {
            this._actorFactoryRef = actorFactoryRef;
        }
        else {
            // Fallback for callers that don't provide the ref.
            var query = new SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY();
            this._actorFactoryRef = server.Ask<SERVER_100_PROTOCOL.MSG_ACTORFACTORYINFO>(query)
                .Result
                .Reference;
        }

        ActorRef = Context.Self;

        CreateSocketActors(socket);
        ConfigureReceivers();
    }

    // Akka.NET ctor
    public static Props Props(Socket socket, ushort sessionId, IActorRef server, IActorRef actorFactoryRef = null)
        => Akka.Actor.Props.Create(() => new SessionActor(socket, sessionId, server, actorFactoryRef));

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
        if (_dispatchTable.TryGetValue(msg.GetType(), out var handlers)) {
            var wasDispatched = false;
            foreach (var handler in handlers) {
                if (handler == Sender) {
                    continue;
                }
                handler.Forward(msg);
                wasDispatched = true;
            }

            if (!wasDispatched) {
                Unhandled(msg);
            }
            return;
        }

        Unhandled(msg);
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
        if (_dispatchTable.TryGetValue(msg.GetType(), out var handlers)) {
            foreach (var handler in handlers) {
                if (handler == Sender) {
                    continue;
                }

                try {
                    return handler.Ask<T>(msg, timeout: TimeSpan.FromSeconds(20)).Result;
                }
                catch (Exception ex) {
                    Logger.Error("SessionActor service attempted to ask another service with {0}, but the timeout " +
                              "was exceeded. {1}", Logger.Args(msg.GetType(), ex.Message));
                }
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
    public void Dispose() => Dispose("shutdown");

    private void Dispose(string reason, bool waitForClientClose = false) {
        // Avoid duplicate Dispose calls.
        if (_isDisposed) {
            // A close that arrives while a logout waits for the client (a kick, a shutdown, the socket closing)
            // ends the session right away.
            StopWaitingForClientClose(reason);

            return;
        }

        Logger.Debug("SessionActor {Id} disposing, reason={Reason}.", Logger.Args(SessionID, reason));
        _isDisposed = true;

        // Send a message to the server to deallocate this SessionActor.
        var msg = new SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET() {
            Id = SessionID,
            Socket = this._socket,
            Ip = this.RemoteIp
        };
        ServerRef.Tell(msg);

        // Don't preemptively close the socket; the client disconnects itself
        // after receiving the final message (e.g. MSG_CHARACTERSELECTED).
        // Closing it here races with any pending SocketSender messages.

        // Dispose services.
        SendPreDisposeToServices();
        SendDisposeToServices();

        Sender.Tell("DoneDisposing");

        if (waitForClientClose) {
            // The client closes its end after the logout echo. The timer only covers a client that never does.
            Logger.Debug("SessionActor {Id} waiting up to {Seconds}s for the client to close.",
                Logger.Args(SessionID, s_clientCloseTimeout.TotalSeconds));
            _clientCloseTimer = Context.System.Scheduler.ScheduleTellOnceCancelable(s_clientCloseTimeout, Self, "ClientCloseTimeout", Self);

            return;
        }

        // Dispose self.
        ActorRef.Tell(PoisonPill.Instance);
    }

    private void StopWaitingForClientClose(string reason) {
        // Also reached from other threads through the public Dispose, so only the actor reference is used.
        var timer = Interlocked.Exchange(ref _clientCloseTimer, null);
        if (timer is null) {
            return;
        }

        timer.Cancel();

        Logger.Debug("SessionActor {Id} closing after logout, reason={Reason}.", Logger.Args(SessionID, reason));
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
                            _stoppedByStrategy = true;
                            return Directive.Stop;
                        }
                    default:
                        Logger.Error("SessionActor {Sid} service {Class} L:{LineNumber} threw unknown exception: " +
                                     "{Message}. Exception details: {Exception}. Inner exception: {InnerException}",
                                     Logger.Args(SessionID, ex.TargetSite?.DeclaringType, ex.TargetSite?.Name, ex.Message, ex, ex.InnerException));
                        return Directive.Resume;
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
        Receive<string>(x => x == "Close", x => Dispose(GetCloseReason()));
        Receive<string>(x => x == "CloseAfterClient", x => Dispose("client logout", waitForClientClose: true));
        Receive<string>(x => x == "ClientCloseTimeout", x => StopWaitingForClientClose("client did not close"));
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
        if (SessionValid) {
            Logger.Debug("SessionActor {Id} ignored a repeated session initialization.", Logger.Args(SessionID));

            return;
        }

        SessionValid = true;

        Logger.Debug("SessionActor {Id} initialized with all services.", Logger.Args(SessionID));

        foreach (var preInitMessage in _preInitMessages) {
            HandlePacket(preInitMessage);
        }
        _preInitMessages.Clear();
    }

    private void SetServices(List<Type> services) {
        // Create handshake services (ControlService) first. Their constructor sends the SessionOffer, the
        // first thing the client waits for on connect, so they must not queue behind the other services'
        // blocking identity Asks in this loop. See IHandshakeService.
        foreach (var service in services.OrderByDescending(t => typeof(IHandshakeService).IsAssignableFrom(t))) {
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

            // Populate the dispatch table.
            foreach (var msgType in identity.MessageHandlers.Keys) {
                if (!_dispatchTable.TryGetValue(msgType, out var list)) {
                    list = [];
                    _dispatchTable[msgType] = list;
                }
                list.Add(childRef);
            }
        }
    }

    private void ReceiveException(Exception ex) {
        Dispose("exception");
    }

    private string GetCloseReason() {
        if (_stoppedByStrategy) {
            return "AllForOne";
        }

        // A close sent by a service names it (ClientService is a client logout); any other sender is a socket actor.
        return _services.TryGetValue(Sender, out var service)
            ? $"{service.GetType().Name} requested close"
            : "socket closed";
    }

    private void HandlePacket(IMessage packet) {
        if (_isDisposed) {
            Logger.Verbose("SessionActor {Id} dropped {MessageName}: the session is closing.",
                Logger.Args(SessionID, packet.GetType().Name));

            return;
        }

        // If the session still is not valid (the client hasn't completed the session handshake)
        // we'll cache all non-control messages for later processing.
        if (!SessionValid && packet.ServiceId != 0) {
            _preInitMessages.Add(packet);

            Logger.Verbose("SessionActor {Id} cached message {MessageName} for later processing.",
                Logger.Args(SessionID, packet.GetType().Name));
                
            return;
        }

        if (_dispatchTable.TryGetValue(packet.GetType(), out var handlers)) {
            foreach (var handler in handlers) {
                handler.Forward(packet);
            }
            return;
        }

        Unhandled(packet);
    }

    private void SendPreDisposeToServices() {
        // Iterate through each service and send them a pre-dispose message. This lets a service gracefully handle
        // the dispose in the case that it requires another service to still be active.
        var pendingReplies = new List<Task>();
        foreach (var (actorRef, type) in _services) {
            // If the service doesn't have a pre-dispose message handler, we'll just skip it.
            if (!type.MessageHandlers.ContainsKey(typeof(SERVICE_101_PROTOCOL.MSG_PREDISPOSE))) {
                continue;
            }

            pendingReplies.Add(actorRef.Ask(new SERVICE_101_PROTOCOL.MSG_PREDISPOSE(), timeout: TimeSpan.FromSeconds(2)));
        }

        // Await the replies under one shared deadline. This is a blocking call so the services dispose gracefully.
        try {
            Task.WaitAll(pendingReplies.ToArray(), 2000);
        }
        catch (AggregateException) {
            // A service that failed or timed out must not keep the others from being disposed.
        }
    }

    private void SendDisposeToServices() {
        // Iterate through our services and send them a dispose message.
        foreach (var (actorRef, type) in _services) {
            actorRef.Tell(new SERVICE_101_PROTOCOL.MSG_DISPOSE());
        }
    }
    
}
