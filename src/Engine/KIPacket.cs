using Imlight.Common.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine
{
    /// <summary>
    /// A packet containing KI protocol message framing.
    /// </summary>
    public class KIPacket
    {

        internal class Header
        {
            internal bool isLarge;
            internal UInt16 length;
            internal UInt32 bigLength;

            internal UInt32 Length()
            {
                if (this.isLarge) return this.bigLength;
                else return this.length;
            }
        }
        internal Header PacketHeader { get; set; }


    }
}
