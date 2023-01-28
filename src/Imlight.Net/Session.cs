using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net
{
    public class Session
    {
        public ushort SessionID { get; init; }
        public bool IsConnected { get; private set; }
        public bool HandshakeValid { get; private set; }
        public bool WaitingForHandshakeResponse { get; private set; }

        public Session(ushort SessionID)
        {
            this.SessionID = SessionID;
            this.IsConnected = true;
            this.HandshakeValid = false;
            this.WaitingForHandshakeResponse = true;
        }

        public void SetHandshakeValid()
        {
            HandshakeValid = true;
            WaitingForHandshakeResponse = false;
            IsConnected = true;
        }
    }
}
