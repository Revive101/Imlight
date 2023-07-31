/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            .Enrich.With(new CallingMethodEnricher())
            .WriteTo.Console(outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Thread}] {CallingMethod} : {Message:lj} {NewLine}{Exception}")
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    internal class ThreadNameEnricher : ILogEventEnricher
    {
        public const string ThreadNamePropertyName = "Thread";
        private const int MaxNameLength = 15;

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
                return name.Length > MaxNameLength 
                    ? name[..MaxNameLength] 
                    : name.PadLeft(MaxNameLength);
            }

            return "Main".PadLeft(MaxNameLength);
        }
    }
    
    internal class CallingMethodEnricher : ILogEventEnricher
    {
        public const string CallingMethodPropertyName = "CallingMethod";
        private const int MaxNameLength = 40;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var frame = new StackFrame(6, false);
            var method = frame.GetMethod();
            if (method == null)
                return;

            var callingMethod = $"{method.ReflectedType?.Name}::{method.Name}";
            if (IsStateMachineMethod(callingMethod))
            {
                callingMethod = GetCallerMethodNameFromAsyncStateMachine(frame);
            }

            var propertyMethod = propertyFactory.CreateProperty(CallingMethodPropertyName, GetConsistentSpacedName(callingMethod));
            logEvent.AddPropertyIfAbsent(propertyMethod);
        }

        private static bool IsStateMachineMethod(string methodName)
        {
            return methodName.Contains('<');
        }

        private static string GetCallerMethodNameFromAsyncStateMachine(StackFrame frame)
        {
            // This is a messy method, but it's a hell of a lot better than going to the call stack. 
            var method = frame.GetMethod();
            if (method == null) 
                return "Unknown";
            
            var declaringType = method.DeclaringType;
            if (declaringType == null) 
                return "Unknown";
            
            // Get the string in between the < and >.
            var stateMachineName = declaringType.Name;
            var startIndex = stateMachineName.IndexOf('<') + 1;
            var length = stateMachineName.LastIndexOf('>') - startIndex;
            var stateMachineMethodName = stateMachineName.Substring(startIndex, length);
            
            // Get the declaring type name between the '.' and '+'. Then, scope it to the last '.'.
            var declaringTypeName = declaringType.FullName;
            startIndex = declaringTypeName.IndexOf('.') + 2;
            length = declaringTypeName.LastIndexOf('+') - startIndex;
            var declaringTypeMethodName = declaringTypeName.Substring(startIndex, length)
                .Split('.')[^1];

            return $"{declaringTypeMethodName}::{stateMachineMethodName}";
        }
        
        private static string GetConsistentSpacedName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return name.Length > MaxNameLength 
                    ? name[..MaxNameLength] 
                    : name.PadRight(MaxNameLength);
            }

            return "Main".PadRight(MaxNameLength);
        }
    }
}
