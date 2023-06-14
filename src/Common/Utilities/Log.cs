/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

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
