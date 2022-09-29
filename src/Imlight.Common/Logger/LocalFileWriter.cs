using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Diagnostics;

namespace Imlight.Common.Logger
{
    internal static class LocalFileWriter
    {

        internal static ObservableQueue<string> WriteQueue = new ObservableQueue<string>();

        private static bool attemptedSubscription = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Writes a message to the local log file.
        /// </summary>
        /// <param name="fullMessage">The full message content.</param>
        internal static void WriteToLogFile(string fullMessage)
        {
            // Subscribe to WriteQueue if not already
            if (!attemptedSubscription) SetWriteQueueSubscriptions();

            // This is all that needs to be done. The event handler will do the work from here.
            WriteQueue.Enqueue(fullMessage);
        }

        private static void SetWriteQueueSubscriptions()
        {
            WriteQueue.CollectionChanged += WriteQueue_CollectionChanged;

            attemptedSubscription = true;
        }

        private static void WriteQueue_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (WriteQueue.Count == 0) return;
            else if (e.Action != NotifyCollectionChangedAction.Add) return;

            string[] s = e.NewItems.Cast<string>().ToArray();
            TimeoutLockWriteToFile(s);
            LockWriteToFile(s);
        }

        /// <summary>
        /// Writes messages to the local file. This is for debug build, and utilizies a diagnostic lock that will throw an exception
        /// upon reaching a hang time of x seconds.
        /// </summary>
        /// <param name="messages">The array of messages to write.</param>
        [Conditional("DEBUG")]
        private static void TimeoutLockWriteToFile(string[] messages)
        {
            // Lock thread using TimeoutLock. TimeoutLock will throw an exception if it takes more than x seconds.
            try
            {
                var n = new object();
                using (new TimeoutLock(n, TimeSpan.FromSeconds(Log.LogToFileTimeout)))
                {
                    using (StreamWriter writer = new StreamWriter($"{Directory.GetCurrentDirectory()}/{Log.LogFileName}.txt", true))
                    {
                        for (int i = 0; i < messages.Length; i++)
                            writer.WriteLine(messages[i]);
                        writer.Close();
                    }

                    WriteQueue.Dequeue();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// Writes messages to the local file. This is for release build, and utilitizes C#'s built in lock functionality.
        /// </summary>
        /// <param name="messages">The array of messages to write.</param>
        [Conditional("RELEASE")]
        private static void LockWriteToFile(string[] messages)
        {
            // Use a normal lock for release builds.
            try
            {
                lock (_lock)
                {
                    using (StreamWriter writer = new StreamWriter($"{Directory.GetCurrentDirectory()}/{Log.LogFileName}.txt", true))
                    {
                        for (int i = 0; i < messages.Length; i++)
                            writer.WriteLine(messages[i]);
                        writer.Close();
                    }

                    WriteQueue.Dequeue();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
    }
}
