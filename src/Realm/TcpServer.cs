using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Net.Sockets;
using System;
using System.Net;
using System.Collections.Generic;
using Imlight.Common;
using Imlight.Common.Logger;
using System.Runtime.InteropServices;
using Imlight.Engine;

/*
Realm
Realms are the bread and butter of this server structure. 
They hold current players, worlds, and zones. 
They are the primary communicator with clients.

For better elaboration, see the RealmManager diagram:
https://app.diagrams.net/#G17utqstWzrlxPp8cVjTZX4e_Hhy8ThKSn
*/

namespace Imlight.Realm
{
    internal class TcpServer : IDisposable
    {

        internal const ushort DEFAULT_PORT = 12000;

        internal List<KISocket> Sockets { get; private set; }
        internal readonly Realm Realm;

        private readonly TcpListener r_listener;
        private readonly CancellationTokenSource r_tokenSource;
        private bool _listening;
        private CancellationToken _token;

        public event EventHandler<RealmDataReceivedEventArgs> OnDataReceived;

        // Constructor
        internal TcpServer(Realm realm, Int32 port = DEFAULT_PORT, bool doAutoStart = true)
        {
            this.Realm = realm;

            // Listen to all IPs
            var ip = IPAddress.Parse("0.0.0.0");
            this.r_listener = new TcpListener(ip, port);
            this.r_tokenSource = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());
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
            this.r_listener.Start();
            this._token = this.r_tokenSource.Token;
            this._listening = true;

            // Begin listen.
            await ListenAsync();
        }

        /// <summary>
        /// Asyncronously listen for incoming connections.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        private async Task ListenAsync()
        {
            // Listen for any incoming sockets and accept data they send.
            while (!_token.IsCancellationRequested)
            {
                if (!this._listening) continue;

                // Accept socket.
                var client = await r_listener.AcceptTcpClientAsync().ConfigureAwait(false);
                Log.Info($"New connection recieved from {client.Client.RemoteEndPoint}.");

                // Create a socket object for the connection, and add it to the connections list.
                KISocket socket = new KISocket(this, client);
                Task.Run(() => socket.OpenListen());
                Sockets.Add(socket);
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        internal void Stop()
        {
            this.r_tokenSource?.Cancel();
        }

        public void Dispose()
        {
            this.Stop();
            foreach (var client in Sockets)
            {
                client.Dispose();
            }
        }

    }
}