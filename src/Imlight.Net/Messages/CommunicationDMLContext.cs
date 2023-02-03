using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler.DML;

namespace Imlight.Net
{
    /// <summary>
    /// Extends INetworkMessages for CommunicationActor context.
    /// </summary>
    public class CommunicationDMLContext
    {
        public CommunicationActor Actor { get; init; }
        public INetworkMessage Message { get; init; }

        public CommunicationDMLContext(CommunicationActor actor, INetworkMessage message)
        {
            Actor = actor;
            Message = message;
        }

        public bool Is(Type type)
        {
            if (this.Message is null) return false;

            return Message.GetType() == type;
        }
    }
}
