using System.Threading.Tasks;
using System;
using System.Net.Sockets;
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
    public class Realm : IDisposable
    {

        public string Name { get; private set; }

        internal TcpServer Server;

        // Constructor
        public Realm(string name, bool autoStart = true)
        {
            this.Name = name;
            this.Server = new TcpServer(TcpServer.DEFAULT_PORT, autoStart);
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

        public void Dispose()
        {
            this.StopServer();
            GC.SuppressFinalize(this);
        }

    }
}