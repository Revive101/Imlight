using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Internals
{
    public interface INetworkMessage
    {
        public byte MessageOrder { get; }
        public byte ServiceID { get; }
    }
}
