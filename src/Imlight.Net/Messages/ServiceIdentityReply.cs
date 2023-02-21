using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Net.Messages
{
    internal class ServiceIdentityReply
    {
        internal ActorMessageService Identity { get; init; }

        public ServiceIdentityReply(ActorMessageService identity)
        {
            this.Identity = identity;
        }
    }
}
