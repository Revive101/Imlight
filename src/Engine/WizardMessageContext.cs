using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine
{
    public class WizardMessageContext
    {

        public byte[] Message { get; }
        public sbyte RealmID { get; }
        public short SocketID { get; }

        // ctor
        public WizardMessageContext(byte[] message, sbyte realmID, short socketID)
        {
            Message = message;
            RealmID = realmID;
            SocketID = socketID;
        }

    }
}
