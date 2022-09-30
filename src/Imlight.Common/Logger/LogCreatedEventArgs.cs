using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Common.Logger
{
    public class LogCreatedEventArgs : EventArgs
    {

        public LogLevel Level { get; set; }
        public string Time { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }

        // ctor
        public LogCreatedEventArgs(LogLevel level, string time, string source, string message)
        {
            Level = level;
            Time = time;
            Source = source;
            Message = message;
        }

    }
}
