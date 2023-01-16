using System;
using System.Collections.Generic;
using System.Timers;
using Imlight.Common;

namespace Imlight.Engine
{
    public static class ProcessorManager
    {

        /// <summary>
        /// The speed of ticks per second.
        /// </summary>
        internal const byte TICK_SPEED = 1;

        internal static List<Processor> Processors { get; private set; } = new List<Processor>();

        // Ticks
        private static bool startedTicks = false;
        private static readonly Timer tickTimer = new Timer(1000 / TICK_SPEED);

        /// <summary>
        /// Start a new processor.
        /// </summary>
        public static void StartNewProcessor() 
        {
            Log.Logger.Information("Starting new processor");
            Processors.Add(new Processor());

            // Do ticks if not already
            if (!startedTicks)
            {
                startedTicks = true;
                tickTimer.Elapsed += ServerTick;
                tickTimer.Start();
            }
        }

        /// <summary>
        /// Start an amount of new processors.
        /// </summary>
        /// <param name="count">The amount of new processors to spawn.</param>
        internal static void StartNewProcessors(short count)
        {
            Log.Logger.Information($"Starting new processors of count {count}.");
            for (int i = 0; i < count; i++)
            {
                Processors.Add(new Processor());

                Log.Logger.Information($"Processor [{count}] started.");
            }
        }

        /// <summary>
        /// Removes a processor from the pool.
        /// </summary>
        internal static void RemoveProcessor()
        {
            if (Processors.Count <= 0)
            {
                Log.Logger.Error("Tried to remove processors when none were active!");
                return;
            }

            // Remove the recently added processor
            Processors.RemoveAt(Processors.Count - 1);
        }

        private static void ServerTick(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < Processors.Count; i++)
            {
                Processors[i].GetWork();
            }
        }
    }
}
