using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Realm
{
    /// <summary>
    /// Event arguments for a realm's TCP server receiving data.
    /// </summary>
    internal class RealmDataReceivedEventArgs : EventArgs
    {

        public byte[] Data { get; }
        public DateTime Time { get; }
        public short SocketID { get; }

        // Constructor
        public RealmDataReceivedEventArgs(byte[] data, short socketID)
        {
            this.Data = data;
            this.Time = DateTime.Now;
            this.SocketID = socketID;
        }

    }
}
