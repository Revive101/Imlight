using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net.Messages
{
    public class ClientConnected
    {
        public Socket Socket { get; init; }

        public ClientConnected(Socket socket)
        {
            Socket = socket;
        }
    }
}
