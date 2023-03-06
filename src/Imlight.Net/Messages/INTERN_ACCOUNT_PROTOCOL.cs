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
        /// <summary>
        /// Sets the account for a service.
        /// </summary>
        public class INTMSG_SET_ACCOUNT : IInternalMessage
        {
            public Account Account;
        }

        /// <summary>
        /// Gets the account from database.
        /// </summary>
        public class INTMSG_GET_ACCOUNT_FROM_DATABASE : IInternalMessage
        {
            public ulong AccountID;
        }

        /// <summary>
        /// Gets the local account from a service.
        /// </summary>
        public class INTMSG_GET_ACCOUNT : IInternalMessage
        {

        }

        /// <summary>
        /// An account object.
        /// </summary>
        public class INTMSG_ACCOUNT : IInternalMessage
        {
            public Account Account;
        }
    }
}
