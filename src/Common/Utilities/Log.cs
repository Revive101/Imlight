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
        private const byte MaxCallerNameLength = 40;
        
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
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Thread}] {CallingSpace} : {Message:lj} {NewLine}{Exception}")
            .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        
        public static void Verbose(string message, 
            object[] args = null,
            [CallerFilePath] string callingClass = "",
            [CallerMemberName] string callingMethod = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Verbose, message, args);
        }
    
        public static void Debug(string message, 
            object[] args = null,
            [CallerFilePath] string callingClass = "", 
            [CallerMemberName] string callingMethod = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Debug, message, args);
        }
    
        public static void Information(string message, 
            object[] args = null,
            [CallerFilePath] string callingClass = "",
            [CallerMemberName] string callingMethod = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Information, message, args);
        }
    
        public static void Warning(string message, 
            object[] args = null,
            [CallerFilePath] string callingClass = "",
            [CallerMemberName] string callingMethod = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Warning, message, args);
        }
    
        public static void Error(string message, 
            object[] args = null,
            [CallerFilePath] string callingClass = "", 
            [CallerMemberName] string callingMethod = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Error, message, args);
        }
    
        public static void Fatal(string message, 
            object[] args = null,
            [CallerFilePath] string callingClass = "", 
            [CallerMemberName] string callingMethod = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            LogEvent(callingClass, callingMethod, lineNumber, LogEventLevel.Fatal, message, args);
        }
    
        private static void LogEvent(string callingClass, string callingMethod, int lineNumber, LogEventLevel logLevel,
            string message, params object[] values)
        {
            callingClass = TrimCallingClass(callingClass);
            var callingSpace = $"{callingClass}.{callingMethod}";
            callingSpace = GetConsistentSpacedName(callingSpace);
            LogContext.PushProperty("CallingSpace", callingSpace);
            Logger.Write(logLevel, message, values);
        }

        public static object[] Args(params object[] args)
        {
            return args;
        }

        private static string TrimCallingClass(string filePath)
        {
            // If the file path doesn't contain a '.cs', then it's not a C# file.
            if (!filePath.Contains(".cs"))
                return filePath;
            
            // Scope to the area between the final '/' character and the '.cs' extension.
            var startIndex = filePath.LastIndexOf('/') + 1;
            var length = filePath.LastIndexOf(".cs", StringComparison.Ordinal) - startIndex;
            return filePath.Substring(startIndex, length);
        }
        
        private static string GetConsistentSpacedName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return name.Length > MaxCallerNameLength 
                    ? name[..MaxCallerNameLength] 
                    : name.PadRight(MaxCallerNameLength);
            }

            return "Main".PadRight(MaxCallerNameLength);
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
