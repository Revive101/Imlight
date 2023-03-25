using Imlight.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.DML;

namespace Imlight.Net.Messages
{
    public sealed class ACCOUNT_104_PROTOCOL : IServerProtocol
    {
        public byte ServiceID { get; } = 104;
        public string ProtocolType { get; } = "ACCOUNT";
        public int ProtocolVersion { get; } = 1;
        public string ProtocolDescription { get; } = "Internal Server Account Messages.";

        public class MSG_QUERYACCOUNT : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 104;
        }
        
        public class MSG_ACCOUNT : IServerMessage
        {
            public byte MessageOrder { get; } = 1;
            public byte ServiceID { get; } = 104;
            
            public Account Account;
        }
    }
}
