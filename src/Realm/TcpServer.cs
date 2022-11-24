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

        // ID, TcpClient
        internal Dictionary<short, TcpClient> Sockets { get; private set; }

        private readonly TcpListener r_listener;
        private readonly CancellationTokenSource r_tokenSource;
        private bool _listening;
        private CancellationToken _token;

        public event EventHandler<RealmDataReceivedEventArgs> OnDataReceived;

        // Constructor
        internal TcpServer(Int32 port = DEFAULT_PORT, bool doAutoStart = true)
        {
            // Listen to all IPs
            var ip = IPAddress.Parse("0.0.0.0");
            this.r_listener = new TcpListener(ip, port);
            this.r_tokenSource = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());
            this.Sockets = new Dictionary<short, TcpClient>();

            if (doAutoStart) Start();
        }

        internal bool Listening() => this._listening;

        /// <summary>
        /// Starts the TCP server.
        /// </summary>
        /// <returns></returns>
        internal void Start()
        {
            this.r_listener.Start();
            this._token = this.r_tokenSource.Token;
            this._listening = true;

            // Begin listen on subtask
            Task.Run(async () => ListenAsync(this._token));
        }

        /// <summary>
        /// Asyncronously listen for incoming connections.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        private async Task ListenAsync(CancellationToken token)
        {
            // Ascrynously listen for any incoming sockets.
            // If an error occurs at any point, drop the listener.
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!this._listening) continue;

                    // Accept socket and add it to connections library.
                    var client = await r_listener.AcceptTcpClientAsync();
                    var id = RandomGen.Unused.SignedNumber(Sockets.Keys);
                    this.Sockets.Add(id, client);

                    // Log
                    Log.Info($"New connection recieved from {client.Client.RemoteEndPoint}");

                    // Invoke event on data received.
                    var stream = client.GetStream();
                    RealmDataReceivedEventArgs args = new RealmDataReceivedEventArgs(stream, id);
                    OnDataReceived?.Invoke(this, args);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"REALM LISTEN ERROR: {ex}");
            }
            finally
            {
                this.r_listener.Stop();
                this._listening = false;
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
        }

    }
}