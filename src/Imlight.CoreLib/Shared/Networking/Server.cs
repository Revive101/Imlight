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
 * SERVER
 * ========================================================================
 * 
 * PURPOSE:
 * Provides a base implementation for network servers using Akka.NET 
 * actor model, managing session creation, connection handling, 
 * and server-level operations.
 * 
 * USAGE EXAMPLE:
 * // Inherit and implement a custom server
 * public class LoginServer : Server {
 *     public LoginServer(string name, int port) : base(name, port, factoryProps) { }
 * }
 * 
 * NOTE:
 * - Abstract base class for network server implementations
 * - Manages active sessions and TCP connections
 * - Provides unique session ID generation and connection handling
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Linq;
using System.Net.Http;
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

    public Server(string name, int port, Props factoryProps, string ip = null) {
        this.Name = name;
        this.Port = port;
        this.ActiveSessions = new ObservableHashSet<SessionActor>();
        this._serverStartTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        this._factoryProps = factoryProps;

        // Use provided IP if set, otherwise auto-detect.
        if (!string.IsNullOrWhiteSpace(ip)) {
            this.Ip = ip;
        }
        else {
#if !DEBUG
            this.Ip = new HttpClient().GetStringAsync("https://api.ipify.org/").Result;
#else
            this.Ip = "127.0.0.1";
#endif
        }

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
