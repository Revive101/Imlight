/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Structures;

namespace Imlight.CoreLib.Shared.Networking;

public abstract class Server : ReceiveProtocolDispatcher {
    public string Name { get; }
    public string Ip { get; }
    public int Port { get; }

    protected readonly ObservableHashSet<SessionActor> ActiveSessions;

    private readonly IActorRef _actorFactoryRef;
    private readonly long _serverStartTime;
    private readonly Props _factoryProps;

    public Server(string name, int port, Props factoryProps) {
        this.Name = name;
        this.Port = port;
        this.ActiveSessions = new ObservableHashSet<SessionActor>();
        this._serverStartTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        this._factoryProps = factoryProps;

        // Get outside IP.
        #if !DEBUG
        this.Ip = new HttpClient().GetStringAsync("https://api.ipify.org/").Result;
        #else
        this.Ip = "127.0.0.1";
        #endif

        CreateTcpListener();
        _actorFactoryRef = CreateActorFactory();
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET))]
    protected virtual void ReceiveAllocateSocket(SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET message) {
        // Create a new child actor, which represents the active socket connection.
        var id = GetNewUniqueId();
        var sessionProps = SessionActor.Props(message.Socket, id, Context.Self);
        Context.ActorOf(sessionProps, $"SessionActor.{id}");

        // Logger
        Logger.Debug("{Type} new connection from {RemoteEndPoint} given session ID {Id}",
            Logger.Args(GetType(), message.Socket.RemoteEndPoint?.ToString(), id));
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET))]
    protected virtual void ReceiveDeallocateSocket(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET message) {
        if (!ActiveSessions.Remove(ActiveSessions.FirstOrDefault(x => x.SessionID == message.Id))) {
            // It's fine if no session was found. This is a common occurrence.
        }
        else {
            Logger.Information("{Name} lost connection to {Ip}", Logger.Args(Name, message.Ip, message.Id));
        }
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY))]
    protected void ReceiveQueryActorFactory(SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY message) {
        var reply = new SERVER_100_PROTOCOL.MSG_ACTORFACTORYINFO() {
            Reference = _actorFactoryRef
        };

        Sender.Tell(reply);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYSERVER))]
    protected void ReceiveQueryServer(SERVER_100_PROTOCOL.MSG_QUERYSERVER message) {
        // Get a list of strings for the connected IPs.
        var ips = ActiveSessions.Select(x => x.RemoteIp).ToArray();
        var msg = new SERVER_100_PROTOCOL.MSG_SERVERINFO() {
            IP = this.Ip,
            Port = Port,
            PlayerCount = (ushort) ActiveSessions.Count,
            ActorRef = Context.Self,
            ConnectedIps = ips
        };

        Sender.Tell(msg);
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        // There is no attempting to stabilize the connection server side. The Wizard101 client will attempt to
        // reconnect on any given failure. This is a good thing, as it allows us to simply stop the session actor
        // and let the client handle the rest.
        new OneForOneStrategy(
            maxNrOfRetries: 1,
            withinTimeRange: TimeSpan.FromSeconds(30),
            localOnlyDecider: ex => {
                // Client regularly shuts down the socket. No need to log it.
                if (ex.Message.ToLower().Contains("shutdown")) {
                    return Directive.Stop;
                }

                Logger.Error("SessionActor {Source} has failed with exception {Exception}",
                    Logger.Args(ex.InnerException.Source, ex));
                return Directive.Stop;
            }
        );

    protected virtual ushort GetNewUniqueId() {
        ushort newId = 0;
        var isUniqueId = false;
        var random = new Random();

        while (!isUniqueId) {
            newId = (ushort) random.Next(ushort.MaxValue);

            if (ActiveSessions.All(s => s.SessionID != newId)) {
                isUniqueId = true;
            }
        }

        return newId;
    }

    private void CreateTcpListener() {
        var actorName = $"{Name}.TcpListener.{Port}";
        var tcpProps = TcpListenerActor.Props(Name, Port, Context.Self);
        Context.ActorOf(tcpProps, actorName);

        Logger.Verbose("New actor created under {Path}: {ActorName}", Logger.Args(Context.Self.Path, actorName));
    }

    private IActorRef CreateActorFactory() {
        if (_factoryProps is null) {
            return null;
        }

        var actorName = $"{Name}.ActorFactory";

        Logger.Verbose("New actor created under {Path}: {ActorName}", Logger.Args(Context.Self.Path, actorName));

        return Context.ActorOf(_factoryProps, actorName);
    }
}
