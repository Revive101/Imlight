using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine
{
    /// <summary>
    /// Extendes a socket's received data to include bonus details about the event.
    /// </summary>
    public class WizardMessageContext
    {

        public byte[] KIPacketBuffer { get; }
        public sbyte RealmID { get; }
        public short SocketID { get; }

        // ctor
        public WizardMessageContext(byte[] message, sbyte realmID, short socketID)
        {
            KIPacketBuffer = message;
            RealmID = realmID;
            SocketID = socketID;
        }

    }
}
