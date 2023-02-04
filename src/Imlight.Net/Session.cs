using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net
{
    public class Session
    {
        public Socket Socket { get; init; }
        public ushort SessionID { get; init; }
        public bool Valid { get; set; }
        public uint SessionStartTime { get; set; }
        public uint SessionMilliseconds { get; set; }
        public DateTime LastActivity { get; set; }

        public Session(Socket socket, ushort sessionID)
        {
            Socket = socket;
            SessionID = sessionID;
            LastActivity = DateTime.Now;
        }
    }
}
