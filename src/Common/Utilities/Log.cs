using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace Imlight.Common.Utilities
{
    public static class Log
    {
        private static readonly ILogger _logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/imlight.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        public static ILogger Logger { get { return _logger; } }
    }
}
