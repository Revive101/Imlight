using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Akka.Actor;
using Imlight.Common;
using Imlight.Net.Messages;

namespace Imlight.Net
{
    public class Server : ReceiveProtocolDispatcher
    {
        public const ushort MAX_PLAYER_COUNT = 20;
        
        public string Name { get; }
        public string IP { get; }
        public int Port { get; }
        public IActorRef TcpListenerActorRef { get; }

        protected Dictionary<ushort, Socket> ConnectedPlayers;
        protected IActorRef ActorFactoryRef;

        private readonly long _serverStartTime;
        private readonly Props _factoryProps;

        public Server(string name, int port, Props factoryProps)
        {
            this.Name = name;
            this.IP = GetLocalIPAddress();
            this.Port = port;
            this.ConnectedPlayers = new Dictionary<ushort, Socket>();
            this._serverStartTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            this._factoryProps = factoryProps;

            TcpListenerActorRef = CreateTcpListener();
            ActorFactoryRef = CreateActorFactory();
        }
        
        public static Props Props(string name, int port, Props factoryProps)
        {
            return Akka.Actor.Props.Create(() => new Server(name, port, factoryProps));
        }
        
        /// <summary>
        /// Gets the time in seconds the server has elapsed.
        /// </summary>
        /// <returns></returns>
        public long ServerElapsed()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds() - _serverStartTime;
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

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET))]
        public void ReceiveAllocateSocket(SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET message)
        {
            var id = GetRandomId();
            var sessionProps = SessionActor.Props(message.Socket, id, Context.Self);
            var actor = Context.ActorOf(sessionProps);

            if (!ConnectedPlayers.TryAdd(id, message.Socket))
            {
                Log.Logger.Error($"Server [{Name}] could not " +
                                 $"add new SessionActor for IP: {message.Socket.RemoteEndPoint}.");
                return;
            }
            
            Log.Logger.Verbose($"Server [{Name}] accepted new SessionActor:" +
                               $"\n\t\tIP: {message.Socket.RemoteEndPoint}" +
                               $"\n\t\tID: {id}");
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET))]
        public void ReceiveDeallocateSocket(SERVER_100_PROTOCOL.MSG_DEALLOCATESOCKET message)
        {
            if (ConnectedPlayers.Remove(message.ID)) return;
            
            Log.Logger.Error($"Server [{Name}] attempted to remove socket by ID [{message.ID}]," +
                             $" but no socket was found.");
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY))]
        public void ReceiveQueryActorFactory(SERVER_100_PROTOCOL.MSG_QUERYACTORFACTORY message)
        {
            var reply = new SERVER_100_PROTOCOL.MSG_QUERIEDACTORFACTORY()
            {
                Reference = ActorFactoryRef
            };
            
            Sender.Tell(reply);
        }
        
        /// <summary>
        /// Generates a unique 2-byte ID.
        /// </summary>
        /// <returns></returns>
        private ushort GetRandomId()
        {
            var rand = new Random();
            while (true)
            {
                var temp = rand.Next(1, ushort.MaxValue);
                if (ConnectedPlayers.Keys.All(x => x != temp))
                {
                    return (ushort)temp;
                }
            }
        }
        
        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}