using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine.DML
{
    public interface INetworkProtocol
    {
        public byte ServiceID { get; }
        public string ProtocolType { get; }
        public Int32 ProtocolVersion { get; }
        public string ProtocolDescription { get; }
    }
}
