using System;

namespace Imlight.Common
{
    /// <summary>
    /// Shares the same functionality as the 'lock' keyword. However, this class will throw a TimeoutException if
    /// the thread remains locked for longer than the given ctor timespan.
    /// </summary>
    public class TimeoutLock : IDisposable
    {

        private object lockObj = new object();

        // ctor
        public TimeoutLock(object lockObj, TimeSpan timeout)
        {
            this.lockObj = lockObj;
            if (!System.Threading.Monitor.TryEnter(this.lockObj, timeout))
                throw new TimeoutException();
        }

        public void Dispose()
        {
            System.Threading.Monitor.Exit(this.lockObj);
        }
    }
}
