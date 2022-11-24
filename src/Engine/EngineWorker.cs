using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Logger;

namespace Imlight.Engine
{
    /// <summary>
    /// EngineProcessor is Imlight.Engine's public entry point for processing messages.
    /// </summary>
    public static class EngineWorker
    {

        /// <summary>
        /// Begins the process of handling a packet.
        /// </summary>
        /// <param name="stream">The NetworkStream containing packet data.</param>
        /// <param name="realmId">The realm ID the packet originates from.</param>
        /// <param name="socketId">The socket ID the packet originiates from.</param>
        public static void AddPacketToWorkload(DataStreamContext context)
        {
            if (MessageFactory.IsKIPacket(context.Stream)) AddWizardPacketToWorkload(context);
            else Log.Warn($"Realm [{context.RealmID}] received non-wizard packet?");
        }

        private static void AddWizardPacketToWorkload(DataStreamContext context)
        {
            WorkloadPool.Add((WizardMessageContext)context);

            Log.Debug($"Data added to Workload Pool from {context.SocketID}");
        }

    }
}
