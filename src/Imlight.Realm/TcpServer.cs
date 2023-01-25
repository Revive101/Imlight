using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Net.Sockets;
using System;
using System.Net;
using System.Collections.Generic;
using Imlight.Common;
using System.Runtime.InteropServices;
using Imlight.Engine;

namespace Imlight.Realm
{
    internal class TcpServer : IDisposable
    {

        internal const ushort DEFAULT_PORT = 12000;

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _tokenSource;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private bool _listening;
        internal List<KISocket> Sockets { get; private set; }
        internal readonly Realm Realm;

        // Constructor
        internal TcpServer(Realm realm, Int32 port = DEFAULT_PORT, bool doAutoStart = true)
        {
            this.Realm = realm;

            // Listen to all IPs
            var ip = IPAddress.Parse("0.0.0.0");
            this._listener = new TcpListener(ip, port);
            this._tokenSource = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());
            this.Sockets = new List<KISocket>();

            if (doAutoStart) Start();
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
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                Log.Logger.Information($"New connection recieved from {client.Client.RemoteEndPoint}.");

                // Create a socket object for the connection, and add it to the connections list.
                var socket = new KISocket(this, client);
                await _semaphore.WaitAsync();
                try
                {
                    Sockets.Add(socket);
                }
                finally
                {
                    _semaphore.Release();
                }

                Task.Run(() => socket.OpenListenAsync(), token);
            }
        }

        internal void Stop()
        {
            this._listening = false;
            this._tokenSource.Cancel();
            _listener.Stop();

            foreach (var client in Sockets)
            {
                client.Close();
            }
        }

        public void Dispose()
        {
            Stop();
            _tokenSource.Dispose();
        }

    }
}