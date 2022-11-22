using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Net.Sockets;
using System;
using System.Net;
using System.Collections.Generic;
using Imlight.Common;

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

        private readonly TcpListener listener;
        private readonly CancellationTokenSource tokenSource;
        private bool listening;
        private CancellationToken token;

        public event EventHandler<RealmDataReceivedEventArgs> OnDataReceived;

        // Constructor
        internal TcpServer(Int32 port = DEFAULT_PORT, bool doAutoStart = true)
        {
            // Listen to all IPs
            var ip = IPAddress.Parse("0.0.0.0");
            this.listener = new TcpListener(ip, port);
            this.tokenSource = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());
            this.Sockets = new Dictionary<short, TcpClient>();

            if (doAutoStart) Start();
        }

        internal bool Listening() => this.listening;

        /// <summary>
        /// Starts the TCP server.
        /// </summary>
        /// <returns></returns>
        internal void Start()
        {
            this.listener.Start();
            this.token = this.tokenSource.Token;
            this.listening = true;

            // Begin listen on subtask
            Task.Run(async () => ListenAsync(this.token));
        }

        /// <summary>
        /// Asyncronously listen for incoming connections.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();

                    var id = RandomGen.Unused.SignedNumber(Sockets.Keys);
                    this.Sockets.Add(id, client);

                    var stream = client.GetStream();

                    // Invoke event on data received.
                    RealmDataReceivedEventArgs args = new RealmDataReceivedEventArgs(stream, id);
                    OnDataReceived?.Invoke(this, args);
                }
                catch (Exception ex)
                {
                    Common.Logger.Log.Error($"REALM LISTEN ERROR: {ex.ToString()}");
                }
                finally
                {
                    this.listener.Stop();
                    this.listening = false;
                }
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        internal void Stop()
        {
            this.tokenSource?.Cancel();
        }

        public void Dispose()
        {
            this.Stop();
        }

    }
}