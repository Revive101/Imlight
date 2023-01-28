using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common;
using Akka.Actor;
using WizUnraveler.Cache;
using WizUnraveler.DML;

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
        protected Dictionary<Session, IActorRef> CommunicationActors { get; init; }
        private long _serverStartTime;

        public ServerReceiverActor(string Name, sbyte ID, ushort port)
        {
            this.Name = Name;
            this.ID = ID;
            this.Server = new TcpServer(Self, port);
            this.CommunicationActors = new Dictionary<Session, IActorRef>();
            this._serverStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            ConfigureReceivers();

            Log.Logger.Information($"ServerReceiverActor [{Name}] with ID [{ID}] created under port [{port}].");
        }

        /// <summary>
        /// Gets Session data from an actor ID.
        /// </summary>
        /// <param name="ID">The Session ID to search for.</param>
        /// <returns>A Session object, if one is found. Null otherwise.</returns>
        public Session GetActorSession(ushort ID)
        {
            var sessionAttempt = CommunicationActors.Keys.First(x => x.SessionID == ID);
            if (sessionAttempt is not null) return sessionAttempt;
            else return null;
        }

        /// <summary>
        /// Gets Session data from an IActorRef.
        /// </summary>
        /// <param name="actorRef">The IActorRef to search for.</param>
        /// <returns>A Session object, if one is found. Null otherwise.</returns>
        public Session GetActorSession(IActorRef actorRef)
        {
            // This is most definitely illegal.
            return CommunicationActors.First(x => x.Value == actorRef).Key;
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

            Receive<GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT>(x => ReceiveClientDisconnect(x));
        }

        /// <summary>
        /// Message handler for RegisterCommunicationActor, which adds incoming CommunicationActors to this Server.
        /// </summary>
        /// <param name="message"></param>
        protected virtual void ReceiveRegisterCommunicationActor(RegisterCommunicationActor message)
        {
            // Create session details for this newly connected CommunicationActor
            var id = GetRandomID();
            var session = new Session(id);

            // Create a new CommunicationActor, and as a name we'll just use the session ID.
            var actorProps = CommunicationActor.Props(message.Socket, id, this);
            var actor = Context.ActorOf(actorProps, id.ToString());
            CommunicationActors.Add(session, actor);

            // This is just for debugging purposes and can be removed for release builds.
            Context.Watch(actor);

            Log.Logger.Verbose($"ServerReceiverActor [{Name}] accepted new CommunicationActor:" +
                $"\n\t\tIP: {message.Socket.RemoteEndPoint}" +
                $"\n\t\tID: {id}");
        }

        private void ReceiveClientDisconnect(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT message)
        {
            Context.Sender.GracefulStop(TimeSpan.FromSeconds(1));
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
                if (!CommunicationActors.Keys.Any(x => x.SessionID == temp))
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
