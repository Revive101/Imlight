using System.Threading.Tasks;
using System;
using System.Net.Sockets;
using Imlight.Common.Logger;
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
    public class Realm : IDisposable
    {

        public string Name { get; private set; }
        public sbyte Id { get; private set; }

        internal TcpServer Server;

        // Constructor
        public Realm(string name, sbyte Id, bool autoStart = true)
        {
            this.Name = name;
            this.Id = Id;
            this.Server = new TcpServer(TcpServer.DEFAULT_PORT, autoStart);

            this.Server.OnDataReceived += Server_OnDataReceived;
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        public void StartServer()
        {
            // Check if the server is already listening.
            if (this.Server.Listening())
            {
                Log.Warn($"Attempted to start already listening realm \"{this.Name}\".");
                return;
            }

            this.Server.Start();
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void StopServer()
        {
            if (!this.Server.Listening())
            {
                Log.Warn($"Attempted to stop non-running realm \"{this.Name}\".");
                return;
            }

            this.Server.Stop();
        }

        public bool IsOpen() => this.Server.Listening();

        private void Server_OnDataReceived(object sender, RealmDataReceivedEventArgs e)
        {
            // Our TCP server has received data and now we must find a processor to send it to.
            //@todo: SocketID

            DataStreamContext context = new DataStreamContext(e.Data, this.Id, e.SocketID);
            EngineWorker.AddPacketToWorkload(context);

            // Log
            var msg = e.Data.ToString();
            if (e.Data.Length >= 100)
            {
                // Concat data if it's too long. We want to keep the console clean.
                msg = msg[..100];
                // Add dots on the end to signify the message was shortened.
                msg += "...";
            }
            Log.Debug($"Realm [{this.Name}] received data: {msg}");
        }

        public void Dispose()
        {
            this.StopServer();
            GC.SuppressFinalize(this);
        }

    }
}