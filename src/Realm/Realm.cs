using System.Threading.Tasks;
using System;
using System.Net.Sockets;

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
            this.Server = new TcpServer();

            if (autoStart) StartServer();
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        public void StartServer()
        {
            if (this.Server.Listening())
            {
                // Log error here
                return;
            }

            Task.Run(async () => this.Server.StartAsync());
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void StopServer()
        {
            if (!this.Server.Listening())
            {
                // Log error here
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