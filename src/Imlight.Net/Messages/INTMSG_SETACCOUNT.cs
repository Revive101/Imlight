using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Data;

namespace Imlight.Net.Messages
{
    public class INTMSG_SETACCOUNT : IInternalMessage
    {
        public Account Account { get; init; }
        public ushort SessionID { get; init; }

        public INTMSG_SETACCOUNT(Account account, ushort sessionID)
        {
            Account = account;
            SessionID = sessionID;
        }
    }
}
