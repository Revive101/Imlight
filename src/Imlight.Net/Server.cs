using System;
using System.Collections.Generic;
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
        public const ushort PLAYER_LIMIT = 40;

        public string Name { get; }
        public string IP { get; }
        public int Port { get; }
        public IActorRef TcpListenerActorRef { get; }
        
        protected readonly Dictionary<ushort, IActorRef> ActiveSessions;
        protected readonly IActorRef ActorFactoryRef;

        private readonly long _serverStartTime;
        private readonly Props _factoryProps;

        public Server(string name, int port, Props factoryProps)
        {
            this.Name = name;
            this.IP = NetUtil.GetLocalIPAddress();
            this.Port = port;
            this.ActiveSessions = new Dictionary<ushort, IActorRef>();
            this._serverStartTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            this._factoryProps = factoryProps;

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
            var id = RandomGen.GenerateUniqueID<ushort>(ActiveSessions.Keys.ToList());
            var sessionProps = SessionActor.Props(message.Socket, id, Context.Self);
            var actor = Context.ActorOf(sessionProps);

            if (!ActiveSessions.TryAdd(id, actor))
            {
                Log.Logger.Error($"Server [{Name}] could not " +
                                 $"add new SessionActor for IP: {message.Socket.RemoteEndPoint}.");
                
                message.Socket.Disconnect(true);
                
                return;
            }
            
            Log.Logger.Verbose($"Server [{Name}] accepted new SessionActor:" +
                               $"\n\t\tIP: {message.Socket.RemoteEndPoint}" +
                               $"\n\t\tID: {id}");
        }

        /// <summary>
        /// Deallocate and disconnect and active socket.
        /// </summary>
        /// <param name="message"></param>
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET))]
        public void ReceiveDeallocateSocket(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET message)
        {
            if (ActiveSessions.Remove(message.ID)) 
                return;
            
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

        private IActorRef CreateTcpListener()
        {
            var tcpProps = TcpListenerActor.Props(Name, Port, Context.Self);
            return Context.ActorOf(tcpProps, $"{Name}_{Port}");
        }

        private IActorRef CreateActorFactory()
        {
            return Context.ActorOf(_factoryProps);
        }
    }
}