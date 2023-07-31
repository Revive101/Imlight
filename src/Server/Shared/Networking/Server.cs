/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Net.Http;
using Akka.Actor;
using Imlight.Common.Structures;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Shared.Networking
{
    public abstract class Server : ReceiveProtocolDispatcher
    {
        // TODO: this sucks here pls move
        public const ushort PLAYER_LIMIT = 10;

        public string Name { get; }
        public string Ip { get; }
        public int Port { get; }

        /// <summary>
        /// The list of active sessions. A session is only considered active if it has been authenticated.
        /// </summary>
        protected readonly ObservableHashSet<SessionActor> ActiveSessions;

        private readonly IActorRef _actorFactoryRef;
        private readonly long _serverStartTime;
        private readonly Props _factoryProps;

        public Server(string name, int port, Props factoryProps)
        {
            this.Name = name;
            this.Port = port;
            this.ActiveSessions = new ObservableHashSet<SessionActor>();
            this._serverStartTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            this._factoryProps = factoryProps;

            // Get outside IP.
            this.Ip = new HttpClient().GetStringAsync("https://api.ipify.org/").Result;
            //this.Ip = "127.0.0.1";

            CreateTcpListener();
            _actorFactoryRef = CreateActorFactory();
        }

        /// <summary>
        /// Gets the time in seconds the server has elapsed.
        /// </summary>
        /// <returns></returns>
        public long ServerElapsed()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds() - _serverStartTime;
        }
        
        public ushort GetPlayerCount()
        {
            return (ushort)ActiveSessions.Count;
        }

        /// <summary>
        /// Process and allocate a new incoming socket connection.
        /// </summary>
        /// <param name="message"></param>
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET))]
        public virtual void ReceiveAllocateSocket(SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET message)
        {
            // Create a new child actor, which represents the active socket connection.
            var id = GetNewUniqueId();
            var sessionProps = SessionActor.Props(message.Socket, id, Context.Self);
            Context.ActorOf(sessionProps, $"SessionActor.{id}");

            // Log
            Log.Logger.Verbose("New actor created under {Path}: SessionActor.{Id}",
                Context.Self.Path, id);
            Log.Logger.Information("{Type} new connection from {RemoteEndPoint} given session ID {Id}",
                GetType(), message.Socket.RemoteEndPoint?.ToString(), id);
        }

        /// <summary>
        /// Deallocate and disconnect and active socket.
        /// </summary>
        /// <param name="message"></param>
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET))]
        public virtual void ReceiveDeallocateSocket(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET message)
        {
            Log.Logger.Information("{Name}.{Port} connection dropped from {Ip} ID: {Id}",
                Name, Port, message.Ip, message.Id);

            foreach (var session in ActiveSessions.ToList()
                         .Where(session => session.SessionID == message.Id))
            {
                ActiveSessions.Remove(session);
                return;
            }

            Log.Logger.Warning("{Name}.{Port} Could not find active session with ID {Id}",
                Name, Port, message.Id);
        }

        /// <summary>
        /// Query the ActorFactory of message services for a SessionActor.
        /// </summary>
        /// <param name="message"></param>
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY))]
        public void ReceiveQueryActorFactory(SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY message)
        {
            var reply = new SERVER_100_PROTOCOL.MSG_ACTORFACTORYINFO()
            {
                Reference = _actorFactoryRef
            };
            
            Sender.Tell(reply);
        }

        /// <summary>
        /// Query information about this server.
        /// </summary>
        /// <param name="message"></param>
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYSERVER))]
        public void ReceiveQueryServer(SERVER_100_PROTOCOL.MSG_QUERYSERVER message)
        {
            var msg = new SERVER_100_PROTOCOL.MSG_SERVERINFO()
            {
                IP = message.IsLocal ? "127.0.0.1" : this.Ip,
                Port = Port,
                PlayerCount = (ushort)ActiveSessions.Count,
                ActorRef = Context.Self,
            };
            
            Sender.Tell(msg);
        }
        
        protected override SupervisorStrategy SupervisorStrategy()
        {
            // There is no attempting to stabilize the connection server side. The Wizard101 client will attempt to
            // reconnect on any given failure. This is a good thing, as it allows us to simply stop the session actor
            // and let the client handle the rest.
            return new OneForOneStrategy(
                maxNrOfRetries: 1,
                withinTimeRange: TimeSpan.FromSeconds(30),
                localOnlyDecider: ex =>
                {
                    return ex switch
                    {
                        _ => Directive.Stop
                    };
                }
            );
        }

        protected virtual ushort GetNewUniqueId()
        {
            ushort newId = 0;
            var isUniqueId = false;
            var random = new Random();

            while (!isUniqueId)
            {
                newId = (ushort)random.Next(ushort.MaxValue);

                if (ActiveSessions.All(s => s.SessionID != newId))
                {
                    isUniqueId = true;
                }
            }

            return newId;
        }
        
        private void CreateTcpListener()
        {
            var actorName = $"{Name}.TcpListener.{Port}";
            var tcpProps = TcpListenerActor.Props(Name, Port, Context.Self);
            Context.ActorOf(tcpProps, actorName);
            
            Log.Logger.Verbose("New actor created under {Path}: {ActorName}",
                Context.Self.Path, actorName);
        }

        private IActorRef CreateActorFactory()
        {
            if (_factoryProps is null) return null;
            
            var actorName = $"{Name}.ActorFactory";
            
            Log.Logger.Verbose("New actor created under {Path}: {ActorName}",
                Context.Self.Path, actorName);
            
            return Context.ActorOf(_factoryProps, actorName);
        }
    }
}
