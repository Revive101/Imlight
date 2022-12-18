using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine
{
    /// <summary>
    /// The workload pool is a pool of messages that have not been yet processed.
    /// </summary>
    public static class WorkloadPool
    {

        internal static Queue<WizardMessageContext> Work = new Queue<WizardMessageContext>();

        /// <summary>
        /// Enqueues work for processors to inevitably handle.
        /// </summary>
        /// <param name="context">The context about the data received.</param>
        public static void Enqueue(WizardMessageContext context)
        {
            Work.Enqueue(context);
        }

        /// <summary>
        /// Clears the workload pool.
        /// </summary>
        public static void ClearQueue() => Work.Clear();

    }
}
