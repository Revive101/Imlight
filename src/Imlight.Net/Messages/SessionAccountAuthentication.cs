using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Data;

namespace Imlight.Net.Messages
{
    public class SessionAccountAuthentication
    {
        public Account Account { get; init; }
        public ushort SessionID { get; init; }

        public SessionAccountAuthentication(Account account, ushort sessionID)
        {
            Account = account;
            SessionID = sessionID;
        }
    }
}
