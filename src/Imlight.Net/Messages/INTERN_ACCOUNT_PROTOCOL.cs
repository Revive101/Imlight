using Imlight.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net.Messages
{
    public class INTERN_ACCOUNT_PROTOCOL
    {
        public class INTMSG_SETACCOUNT : IInternalMessage
        {
            public Account Account;
        }

        public class INTMSG_GETACCOUNT : IInternalMessage
        {
            public ulong AccountID;
        }

        public class INTMSG_ACCOUNT : IInternalMessage
        {
            public Account Account;
        }
    }
}
