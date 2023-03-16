using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net.Messages;

namespace Imlight.Net
{
    public abstract class Server : ReceiveProtocolDispatcher
    {
        public const ushort PLAYER_LIMIT = 1;

        public string Name { get; }
        public string IP { get; }
        public int Port { get; }
        public IActorRef TcpListenerActorRef { get; }
        
        protected readonly ObservableHashSet<SessionActor> ActiveSessions;
        protected readonly ListQueue<SessionActor> PlayerQueue;
        protected readonly IActorRef ActorFactoryRef;

        private readonly long _serverStartTime;
        private readonly Props _factoryProps;

        public Server(string name, int port, Props factoryProps)
        {
            this.Name = name;
            this.IP = NetUtil.GetLocalIPAddress();
            this.Port = port;
            this.ActiveSessions = new ObservableHashSet<SessionActor>();
            this.PlayerQueue = new ListQueue<SessionActor>();
            this._serverStartTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            this._factoryProps = factoryProps;
            
            // Create events.
            this.ActiveSessions.CollectionChanged += ActiveSessionsChangedEvent;

            TcpListenerActorRef = CreateTcpListener();
            ActorFactoryRef = CreateActorFactory();
        }

        /// <summary>
        /// Gets the time in seconds the server has elapsed.
        /// </summary>
        /// <returns></returns>
        public long ServerElapsed()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds() - _serverStartTime;
        }

        /// <summary>
        /// Process and allocate a new incoming socket connection.
        /// </summary>
        /// <param name="message"></param>
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET))]
        public void ReceiveAllocateSocket(SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET message)
        {
            // Create a new child actor, which represents the active socket connection.
            var id = GetNewUniqueId();
            var sessionProps = SessionActor.Props(message.Socket, id, Context.Self);
            var actor = Context.ActorOf(sessionProps);

            // Log
            Log.Logger.Information($"{this.GetType()} new connection " +
                                   $"from {message.Socket.RemoteEndPoint} ");
        }

        /// <summary>
        /// Deallocate and disconnect and active socket.
        /// </summary>
        /// <param name="message"></param>
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET))]
        public void ReceiveDeallocateSocket(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET message)
        {
            // Log
            Log.Logger.Information($"{this.GetType()} connection dropped " +
                                   $"from {message.Socket.RemoteEndPoint} ");
            
            foreach (var session in ActiveSessions.ToList())
            {
                if (session.SessionID != message.ID)
                    continue;

                ActiveSessions.Remove(session);
            }
            
            // If we couldn't find them in the active sessions, it might be possible they're in the queue.
            foreach (var session in PlayerQueue.ToList())
            {
                if (session.SessionID != message.ID)
                    continue;

                PlayerQueue.Remove(session);
            }
            
            Log.Logger.Error($"Server [{Name}] attempted to remove socket by ID [{message.ID}]," +
                             $" but no socket was found.");
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
                Reference = ActorFactoryRef
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
                IP = IP,
                Port = Port,
                PlayerCount = (ushort)ActiveSessions.Count,
                ActorRef = Context.Self,
            };
            
            Sender.Tell(msg);
        }

        protected abstract void ActiveSessionsChangedEvent(object obj, NotifyCollectionChangedEventArgs args);
        
        private IActorRef CreateTcpListener()
        {
            var tcpProps = TcpListenerActor.Props(Name, Port, Context.Self);
            return Context.ActorOf(tcpProps, $"{Name}_{Port}");
        }

        private IActorRef CreateActorFactory()
        {
            return Context.ActorOf(_factoryProps);
        }

        private ushort GetNewUniqueId()
        {
            ushort newId = 0;
            var isUniqueId = false;
            var random = new Random();

            while (!isUniqueId)
            {
                newId = (ushort)random.Next(ushort.MaxValue);

                if (!ActiveSessions.Any(s => s.SessionID == newId) 
                    && !PlayerQueue.Any(s => s.SessionID == newId))
                {
                    isUniqueId = true;
                }
            }

            return newId;
        }

    }
}