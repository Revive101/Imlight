using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Engine
{
    /// <summary>
    /// The workload pool is a pool of messages that have not been processed.
    /// </summary>
    internal static class WorkloadPool
    {

        internal static Queue<WizardMessageContext> Work = new Queue<WizardMessageContext>();

        internal static void Add(WizardMessageContext context)
        {
            Work.Enqueue(context);
        }

    }
}
