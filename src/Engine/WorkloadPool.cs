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

        public static void Enqueue(WizardMessageContext context)
        {
            Work.Enqueue(context);
        }

    }
}
