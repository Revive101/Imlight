/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Imlight.Common.Utilities
{
    public static class Log
    {
        private const byte MaxThreadNameLength = 15;
        
        // TODO: Make this configurable.
        private static readonly string _path = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? string.Empty, "log.txt");

        public static ILogger Logger { get; } = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.With(new ThreadIdEnricher())
            //.Enrich.With(new CallingMethodEnricher())
            .WriteTo.Console(outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Thread}] {CallingClass}.{CallingMethod} : {Message:lj} {NewLine}{Exception}")
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        public static void Verbose(string message, 
            [CallerFilePath] string callingClass = "",
            [CallerMemberName] string callingMethod = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Verbose, message);
        }
    
        public static void Debug(string message, 
            [CallerFilePath] string callingClass = "", 
            [CallerMemberName] string callingMethod = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Debug, message);
        }
    
        public static void Information(string message, 
            [CallerFilePath] string callingClass = "",
            [CallerMemberName] string callingMethod = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Information, message);
        }
    
        public static void Warning(string message, 
            [CallerFilePath] string callingClass = "",
            [CallerMemberName] string callingMethod = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Warning, message);
        }
    
        public static void Error(string message, 
            [CallerFilePath] string callingClass = "", 
            [CallerMemberName] string callingMethod = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Error, message);
        }
    
        public static void Fatal(string message, 
            [CallerFilePath] string callingClass = "", 
            [CallerMemberName] string callingMethod = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Fatal, message);
        }
    
        private static void LogEvent(string callingClass, string callingMethod, int lineNumber, LogEventLevel logLevel,
            string message)
        {
            callingClass = TrimCallingClass(callingClass);
            LogContext.PushProperty("CallingClass", callingClass);
            LogContext.PushProperty("CallingMethod", $"{callingMethod}");
            Logger.Write(logLevel, message);
        }

        private static string TrimCallingClass(string filePath)
        {
            // Scope to the area between the final '/' character and the '.cs' extension.
            var startIndex = filePath.LastIndexOf('/') + 1;
            var length = filePath.LastIndexOf(".cs", StringComparison.Ordinal) - startIndex;
            return filePath.Substring(startIndex, length);
        }
    }

    /// <summary>
    /// Reflects over the current environment to get a consistent thread name for context enrichment.
    /// </summary>
    internal class ThreadIdEnricher : ILogEventEnricher
    {
        public const string ThreadNamePropertyName = "Thread";
        private const int MaxNameLength = 2;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var threadName = GetConsistentSpacedName(Environment.CurrentManagedThreadId.ToString());
            if (string.IsNullOrEmpty(threadName))
                threadName = $"Thread-{Environment.CurrentManagedThreadId}";

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
    
    /// <summary>
    /// Reflects over the call stack to get a calling source for context enrichment. This is mighty expensive and
    /// unreliable, but exists as a last resort.
    /// </summary>
    internal class CallingMethodEnricher : ILogEventEnricher
    {
        private const string CallingMethodPropertyName = "CallingMethod";
        private const int MaxNameLength = 40;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var frame = new StackFrame(7, false);
            var method = frame.GetMethod();
            var callingMethod = "Imlight";

            if (method != null)
            {
                callingMethod = $"{method.ReflectedType?.Name}::{method.Name}";
                if (IsStateMachineMethod(callingMethod))
                {
                    callingMethod = GetCallerMethodNameFromAsyncStateMachine(frame);
                }
            }

            AddCallingMethodProperty(logEvent, propertyFactory, callingMethod);
        }

        private static void AddCallingMethodProperty(LogEvent logEvent, ILogEventPropertyFactory propertyFactory,
            string callingMethod)
        {
            var paddedName = GetConsistentSpacedName(callingMethod);
            var propertyMethod = propertyFactory.CreateProperty(CallingMethodPropertyName, paddedName);
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
