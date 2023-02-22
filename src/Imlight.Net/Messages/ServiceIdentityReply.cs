using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net.Messages
{
    public class ServiceIdentityReply
    {
        public MessageService Identity { get; init; }

        public ServiceIdentityReply(MessageService identity)
        {
            this.Identity = identity;
        }
    }
}
