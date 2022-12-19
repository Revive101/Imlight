using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Internals
{
    public class ControlMessages : INetworkProtocol
    {

        public byte ServiceID => 0;
        public string ProtocolType => "CONTROL";
        public int ProtocolVersion => 1;
        public string ProtocolDescription => "Responsible for general network session management";

        public class SessionOffer : INetworkMessage
        {
            public byte MessageOrder => 0;
            [DMLElement("USHRT")] public ushort SessionID;
            [DMLElement("UINT")] public uint Unknown1; // Possibly the upper 4 bytes of the timestamp ?
            [DMLElement("UINT")] public uint Timestamp;
            [DMLElement("UINT")] public uint Milliseconds;
        }

        public class KeepAlive : INetworkMessage
        {
            public byte MessageOrder => 3;
            [DMLElement("USHRT")] public ushort SessionID;
            [DMLElement("USHRT")] public ushort Milliseconds; // SessionOffer uses 32 bits but this one uses 16 ??
            [DMLElement("USHRT")] public ushort Seconds;
        }

        public class KeepAliveResponse : INetworkMessage
        {
            public byte MessageOrder => 4;
            [DMLElement("USHRT")] public ushort Unknown1;
            [DMLElement("UINT")] public uint Timestamp;
        }

        public class SessionAccept : INetworkMessage
        {
            public byte MessageOrder => 5;
            [DMLElement("USHRT")] public ushort Reserved1;
            [DMLElement("UINT")] public uint Unknown1; // Possible the upper 4 bytes of the timestamp ?
            [DMLElement("UINT")] public uint Timestamp;
            [DMLElement("UINT")] public uint Milliseconds;
            [DMLElement("USHRT")] public ushort SessionID;
        }

        public INetworkMessage Dispatch(byte id)
        {
            return id switch
            {
                (0) => new SessionOffer(),
                (3) => new KeepAlive(),
                (4) => new KeepAliveResponse(),
                (5) => new SessionAccept(),
                _ => throw new InternalException($"Control message by ID [{id}] was not found!"),
            };
        }

    }
}
