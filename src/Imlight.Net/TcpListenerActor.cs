using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using Imlight.Common;

namespace Imlight.Net
{
    public class TcpListenerActor : ReceiveActor
    {
        public string Name { get; init; }
        public bool Listening { get; private set; }

        protected ConcurrentDictionary<ushort, IActorRef> CommunicationActors { get; init; }

        private readonly long _serverStartTime;
        private int _port;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _tokenSource;
        private readonly Props _actorFactoryProps;
        private IActorRef _actorFactoryRef;

        public TcpListenerActor(string name, int port, Props actorFactoryProps)
        {
            this.Name = name;
            this._port = port;
            this._serverStartTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            this._listener = new TcpListener(IPAddress.Parse("0.0.0.0"), port);
            this._tokenSource = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());
            this.CommunicationActors = new ConcurrentDictionary<ushort, IActorRef>();
            this._actorFactoryProps = actorFactoryProps;

            Start();
        }

        public static Props Props(string name, int port, Props actorFactoryProps)
        {
            return Akka.Actor.Props.Create(() => new TcpListenerActor(name, port, actorFactoryProps));
        }

        protected override void PreStart()
        {
            _actorFactoryRef = Context.ActorOf(_actorFactoryProps, "LoginServiceFactory");

            Log.Logger.Debug($"TcpListenerActor {Name} PreStart complete.");

            base.PreStart();
        }

        public async void Start()
        {
            Log.Logger.Information($"TcpListenerActor {Name} starting..");

            this.Listening = true;
            this._listener.Start();

            var token = this._tokenSource.Token;

            Log.Logger.Information($"TcpListenerActor {Name} started at {_serverStartTime}! Beginning listen on port {_port}.");

            await ListenAsync(token, Context);
        }

        public void Stop()
        {
            this.Listening = false;
            this._tokenSource.Cancel();
            _listener.Stop();

            Log.Logger.Information($"TcpListenerActor {Name} stopped.");
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
        /// Asyncronously listen for incoming connections.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        protected async Task ListenAsync(CancellationToken token, IUntypedActorContext context)
        {
            // Listen for any incoming sockets and accept data they send.
            while (!token.IsCancellationRequested)
            {
                if (!this.Listening) continue;

                // Accept socket and create a new SessionActor for them.
                var socket = await _listener.AcceptSocketAsync();
                CreateSessionActor(socket, context);
            }
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

        private void CreateSessionActor(Socket socket, IUntypedActorContext context)
        {
            var id = GetRandomID();
            var actorProps = SessionActor.Props(socket, id, _actorFactoryRef);
            var actor = context.ActorOf(actorProps, id.ToString());

            if (!CommunicationActors.TryAdd(id, actor))
            {
                Log.Logger.Error($"TcpListenerActor [{Name}] could not add new CommunicationActor for IP: {socket.RemoteEndPoint}.");
                return;
            }

            Log.Logger.Verbose($"TcpListenerActor [{Name}] accepted new CommunicationActor:" +
                $"\n\t\tIP: {socket.RemoteEndPoint}" +
                $"\n\t\tID: {id}");
        }
    }
}
