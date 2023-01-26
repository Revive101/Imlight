using Akka.Actor;
using Imlight.Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Imlight.Realm.Messages;

namespace Imlight.Realm
{
    internal class TcpServer : IDisposable
    {
        internal const ushort DEFAULT_PORT = 12000;

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _tokenSource;
        private bool _listening;
        internal readonly IActorRef Realm;

        // Constructor
        internal TcpServer(IActorRef realm, int port = DEFAULT_PORT)
        {
            this.Realm = realm;

            // Listen to all IPs
            var ip = IPAddress.Parse("0.0.0.0");
            this._listener = new TcpListener(ip, port);
            this._tokenSource = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());

            Start();
        }

        internal bool Listening() => this._listening;

        /// <summary>
        /// Starts the TCP server.
        /// </summary>
        /// <returns></returns>
        internal async void Start()
        {
            this._listening = true;
            this._listener.Start();
            var token = this._tokenSource.Token;

            await ListenAsync(token);
        }

        /// <summary>
        /// Asyncronously listen for incoming connections.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        private async Task ListenAsync(CancellationToken token)
        {
            // Listen for any incoming sockets and accept data they send.
            while (!token.IsCancellationRequested)
            {
                if (!this._listening) continue;

                // Accept socket.
                var socket = await _listener.AcceptSocketAsync();
                Realm.Tell(new RegisterCommunicationActor(socket));
            }
        }

        internal void Stop()
        {
            this._listening = false;
            this._tokenSource.Cancel();
            _listener.Stop();
        }

        public void Dispose()
        {
            Stop();
            _tokenSource.Dispose();
        }
    }
}