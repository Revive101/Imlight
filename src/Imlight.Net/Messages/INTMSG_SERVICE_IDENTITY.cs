using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net.Messages
{
    public class INTMSG_SERVICE_IDENTITY : IInternalMessage
    {
        public MessageService Identity { get; init; }

        public INTMSG_SERVICE_IDENTITY(MessageService identity)
        {
            this.Identity = identity;
        }
    }
}
