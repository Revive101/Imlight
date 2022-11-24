using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Collections.Specialized;
using Imlight.Common;
using Imlight.Common.Logger;

namespace Imlight.Engine
{
    internal class Processor : IDisposable
    {

        internal const byte MAX_WORKLOAD_COUNT = 5;

        private readonly string _name;

        /*
         * Tasks could potentially take more than one server tick to calculate.
         * To solve this, each processor contains an internal, small workload.
         */
        public ObservableQueue<WizardMessageContext> InternalWorkload { get; private set; }  = new ObservableQueue<WizardMessageContext>();

        // ctor
        public Processor()
        {
            this._name = RandomGen.String(5);

            // Subscribe to events
            InternalWorkload.CollectionChanged += InternalWorkload_CollectionChanged;

            // Log completion
            Log.Info($"Startup for processor [{this._name}] complete.");
        }

        /// <summary>
        /// Gets work from the WorkloadPool.
        /// </summary>
        internal void GetWork()
        {
            // Make sure this processor isn't trying to handle too much data.
            if (InternalWorkload.Count >= MAX_WORKLOAD_COUNT) return;

            // Get work, if exists.
            WizardMessageContext work = WorkloadPool.Work.Peek();
            if (work != null)
            {
                WorkloadPool.Work.Dequeue();
                this.InternalWorkload.Enqueue(work);
            }
        }

        private void InternalWorkload_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Do work

        }

        void IDisposable.Dispose()
        {
            //@todo
            throw new NotImplementedException();
        }

    }
}