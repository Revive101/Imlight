using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Net.Sockets;
using System;
using System.Net;
using System.Collections.Generic;

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

        internal List<TcpClient> Sockets { get; private set; }

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
            this.Sockets = new List<TcpClient>();

            if (doAutoStart) Task.Run(async () => await StartAsync());
        }

        internal bool Listening() => this.listening;

        /// <summary>
        /// Asyncronously start the TCP server.
        /// </summary>
        /// <returns></returns>
        internal async Task StartAsync()
        {
            this.token = this.tokenSource.Token;
            listener.Start();
            this.listening = true;

            // Begin listen on subtask
            await Task.Run(async () => ListenAsync(this.token));
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
                    var client = this.listener.AcceptTcpClientAsync();
                    var result = client.Result;

                    this.Sockets.Add(result);

                    OnDataReceived?.Invoke(this, new RealmDataReceivedEventArgs(result.GetStream().ToString()));
                }
                catch (Exception ex)
                {
                    // Log error here
                    int i = 0;
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