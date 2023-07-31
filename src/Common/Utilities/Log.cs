/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.IO;
using System.Reflection;
using Serilog;

namespace Imlight.Common.Utilities
{
    public static class Log
    {
        // TODO: Make this configurable.
        private static readonly string _path = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? string.Empty, $"log.txt");
        
        private static readonly ILogger _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(_path, rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        public static ILogger Logger { get { return _logger; } }
    }
}
