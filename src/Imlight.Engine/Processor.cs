using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Collections.Specialized;
using Imlight.Common;
using Imlight.Internals;
using Imlight.IO;

namespace Imlight.Engine
{
    internal class Processor : IDisposable
    {

        internal const byte MAX_WORKLOAD_COUNT = 25;

        private readonly string _name;

        /*
         * Tasks could potentially take more than one server tick to calculate.
         * To solve this, each processor contains an internal, small workload.
         */
        public ObservableQueue<WizardMessageContext> InternalWorkload { get; private set; } = new ObservableQueue<WizardMessageContext>();

        // ctor
        public Processor()
        {
            this._name = RandomGen.String(5);

            // Subscribe to events
            InternalWorkload.CollectionChanged += WorkOnData;

            // Log completion
            Log.Logger.Information($"Startup for processor [{this._name}] complete.");
        }

        /// <summary>
        /// Gets work from the WorkloadPool.
        /// </summary>
        internal void GetWork()
        {
            // The ProcessorManager will call this method for each processor once every server tick.

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

        private void WorkOnData(object sender, NotifyCollectionChangedEventArgs e)
        {
            // If there is work in the workload pool, it's already verified as a KI packet.
            // Starting from here though, it's nothing but a byte stream. It needs to be deserialized first.

            // Deserialize into a DMLRecord object.
            // @TODO: Change this to process all new items rather than just the first.
            var workingItem = (WizardMessageContext)e.NewItems[0];
            var buffer = workingItem.KIPacketBuffer;
            INetworkMessage message = MessageSerializer.DeserializeMessageBinary(buffer);

            // Log
            Log.Logger.Debug($"Processor [{_name}] picked up packet [{message.GetType()}]");

        }

        public void Dispose()
        {
            InternalWorkload.Clear();
            InternalWorkload = null;
            GC.SuppressFinalize(this);
        }

    }
}