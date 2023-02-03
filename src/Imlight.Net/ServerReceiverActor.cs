using Akka.Actor;
using Imlight.Common;
using Imlight.Net.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using WizUnraveler.Cache;

namespace Imlight.Net
{
    /// <summary>
    /// Represents a networked ReceiveActor with an active TcpServer.
    /// </summary>
    public abstract class ServerReceiverActor : ReceiveActor
    {
        public string Name { get; init; }
        public sbyte ID { get; init; }
        protected TcpServer Server { get; init; }
        protected Dictionary<ushort, IActorRef> CommunicationActors { get; init; }
        private long _serverStartTime;

        public ServerReceiverActor(string Name, sbyte ID, ushort port)
        {
            this.Name = Name;
            this.ID = ID;
            this.Server = new TcpServer(Self, port);
            this.CommunicationActors = new Dictionary<ushort, IActorRef>();
            this._serverStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            ConfigureReceivers();

            Log.Logger.Information($"ServerReceiverActor [{Name}] with ID [{ID}] created under port [{port}].");
        }

        /// <summary>
        /// Gets the time in milliseconds the server has elapsed.
        /// </summary>
        /// <returns></returns>
        public long ServerElapsed()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds() - _serverStartTime;
        }

        /// <summary>
        /// Configures all Server receivers. If overridden, highly recommended as to keep `base.ConfigureRecivers()`, otherwise
        /// CommunicationActors may not be registered.
        /// </summary>
        protected virtual void ConfigureReceivers()
        {
            Receive<RegisterCommunicationActor>(x => ReceiveRegisterCommunicationActor(x));
            Receive<ClientConnected>(x => ReceiveClientConnected(x));
            Receive<Terminated>(t => Log.Logger.Debug($"Actor [{t.ActorRef.Path}] terminated."));

            // Respond to generic ping messages.
            Receive<CommunicationDMLContext>(x => x.Is(typeof(SYSTEM_1_PROTOCOL.MSG_PING)), x =>
            {
                Sender.Tell(new SYSTEM_1_PROTOCOL.MSG_PING_RSP());
            });
        }

        /// <summary>
        /// Message handler for RegisterCommunicationActor, which adds incoming CommunicationActors to this Server.
        /// </summary>
        /// <param name="message"></param>
        protected virtual void ReceiveRegisterCommunicationActor(RegisterCommunicationActor message)
        {
            // Create a new CommunicationActor, and as a name we'll just use the session ID.
            var id = GetRandomID();
            var actorProps = CommunicationActor.Props(message.Socket, id, this);
            var actor = Context.ActorOf(actorProps, id.ToString());
            CommunicationActors.Add(id, actor);

            // This is just for debugging purposes and can be removed for release builds.
            Context.Watch(actor);

            Log.Logger.Verbose($"ServerReceiverActor [{Name}] accepted new CommunicationActor:" +
                $"\n\t\tIP: {message.Socket.RemoteEndPoint}" +
                $"\n\t\tID: {id}");
        }

        /// <summary>
        /// Message handler for ClientConnected, which is received when a CommunicationActor successfully initializes a session.
        /// </summary>
        /// <param name="message"></param>
        protected virtual void ReceiveClientConnected(ClientConnected message)
        {
            // Log
            var ip = message.Socket.RemoteEndPoint.ToString();
            Log.Logger.Information($"ServerReceiverActor [{Name}] client connected from [{ip}]");
        }

        /// <summary>
        /// Generates a unique CommunicationActor ID.
        /// </summary>
        /// <returns></returns>
        protected ushort GetRandomID()
        {
            var rand = new Random();
            while (true)
            {
                var temp = rand.Next(1, ushort.MaxValue);
                if (!CommunicationActors.Keys.Any(x => x == temp))
                {
                    return (ushort)temp;
                }
                else continue;
            }
        }

        protected override void Unhandled(object message)
        {
            base.Unhandled(message);

            Log.Logger.Error($"ServerReceiverActor [{Name}] cannot handle message of type [{message.GetType()}]");
        }
    }
}