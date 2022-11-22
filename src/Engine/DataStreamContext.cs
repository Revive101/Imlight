using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine
{
    public class DataStreamContext
    {

        public NetworkStream Stream;
        public sbyte RealmID;
        public short SocketID;

        public DataStreamContext(NetworkStream stream, sbyte realmID, short socketID)
        {
            Stream = stream;
            RealmID = realmID;
            SocketID = socketID;
        }
    }
}
