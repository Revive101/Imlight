using Akka.Actor;
using Imlight.Common;
using Imlight.Net.Messages;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WizUnraveler.Cache;
using WizUnraveler.DML;

namespace Imlight.Net
{
    /// <summary>
    /// Represents a networked ReceiveActor with an active TcpServer.
    /// </summary>
    public abstract class ServerReceiverActor : ReceiveActor
    {
        public const int AFK_TIMEOUT = 300;       // In seconds.
        public const int AFK_CHECK_INTERVAL = 30; // In seconds.

        public string Name { get; init; }
        public sbyte ID { get; init; }
        protected TcpServer Server { get; init; }
        protected ConcurrentDictionary<IActorRef, Session> CommunicationActors { get; init; }

        private long _serverStartTime;
        private CancellationTokenSource _afkCancelToken;

        public ServerReceiverActor(string Name, sbyte ID, ushort port)
        {
            this.Name = Name;
            this.ID = ID;
            this.Server = new TcpServer(Self, port);
            this.CommunicationActors = new ConcurrentDictionary<IActorRef, Session>();
            this._serverStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // Start the AFK detection task
            _afkCancelToken = new CancellationTokenSource();
            Task.Factory.StartNew(CheckAFK);

            ConfigureReceivers();

            Log.Logger.Information($"ServerReceiverActor [{Name}] with ID [{ID}] created under port [{port}].");
        }

        protected override void Unhandled(object message)
        {
            Log.Logger.Error($"ServerReceiverActor [{Name}] cannot handle message of type [{message.GetType()}]");
        }

        protected override void PostStop()
        {
            _afkCancelToken.Cancel();

            base.PostStop();
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
            Receive<INetworkMessage>(x => ConfigureSessionActivity());
            Receive<RegisterCommunicationActor>(x => ReceiveRegisterCommunicationActor(x));
            Receive<ClientConnected>(x => ReceiveClientConnected(x));
            Receive<Terminated>(t => Log.Logger.Debug($"Actor [{t.ActorRef.Path}] terminated."));

            // Respond to generic ping messages.
            Receive<SYSTEM_1_PROTOCOL.MSG_PING>(x =>
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
            // Create a new CommunicationActor.
            var id = GetRandomID();
            var session = new Session(message.Socket, id);
            var actorProps = CommunicationActor.Props(session, this);
            var actor = Context.ActorOf(actorProps, id.ToString());
            if (!CommunicationActors.TryAdd(actor, session))
            {
                Log.Logger.Error($"ServerReceiverActor [{Name}] could not add new CommunicationActor.");
                return;
            }

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
                if (!CommunicationActors.Values.Any(x => x.SessionID == temp))
                {
                    return (ushort)temp;
                }
                else continue;
            }
        }

        protected bool TryGetSession(IActorRef actorRef, out Session session)
        {
            return CommunicationActors.TryGetValue(actorRef, out session);
        }

        private bool ConfigureSessionActivity()
        {
            // If any message is received, restart our AFK timer for the session.

            if (!TryGetSession(Context.Sender, out var session))
            {
                Log.Logger.Error($"ServerReceiverActor [{Name}] could not configure latest session activity, " +
                    $"because the session was not found!");
                return false;
            }

            session.LastActivity = DateTime.Now;

            return false;
        }

        private async Task CheckAFK()
        {
            while (!_afkCancelToken.IsCancellationRequested)
            {
                foreach (var actor in CommunicationActors)
                {
                    if (DateTime.Now - actor.Value.LastActivity <= TimeSpan.FromSeconds(AFK_CHECK_INTERVAL))
                        continue;

                    actor.Key.Tell("Close");
                    CommunicationActors.TryRemove(actor);
                }

                await Task.Delay(TimeSpan.FromSeconds(AFK_CHECK_INTERVAL), _afkCancelToken.Token);
            }
        }
    }
}