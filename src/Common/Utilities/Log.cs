/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.IO;
using System.Reflection;
using System.Threading;
using Serilog;
using Serilog.Core;
using Serilog.Enrichers;
using Serilog.Events;

namespace Imlight.Common.Utilities
{
    public static class Log
    {
        private const byte MaxThreadNameLength = 15;
        
        // TODO: Make this configurable.
        private static readonly string _path = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? string.Empty, $"log.txt");

        public static ILogger Logger { get; } = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .Enrich.With(new ThreadNameEnricher())
            .WriteTo.Console(outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Thread}] [{Level:u5}] {Message:lj} {NewLine}{Exception}")
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    internal class ThreadNameEnricher : ILogEventEnricher
    {
        public const string ThreadNamePropertyName = "Thread";
        private const int MaxThreadNameLength = 15;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var threadName = GetConsistentSpacedName(Thread.CurrentThread.Name);
            if (string.IsNullOrEmpty(threadName))
                threadName = $"Thread-{System.Environment.CurrentManagedThreadId}";

            var property = propertyFactory.CreateProperty(ThreadNamePropertyName, threadName);
            logEvent.AddPropertyIfAbsent(property);
        }

        private static string GetConsistentSpacedName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return name.Length > MaxThreadNameLength 
                    ? name[..MaxThreadNameLength] 
                    : name.PadLeft(MaxThreadNameLength);
            }

            return "Main".PadLeft(MaxThreadNameLength);
        }
    }
}
