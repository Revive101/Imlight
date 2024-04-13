/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Imlight.Common.Configuration;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Imlight.Common;

public class Logger {
    private const byte MaxCallerNameLength = 40;
    private static readonly string s_path = ConfigurationManager.Settings.LogPath;
    private static readonly string s_logFormat = ConfigurationManager.Settings.LogFormat;
    private static readonly string s_logLevel = ConfigurationManager.Settings.LogLevel;
    private static readonly string s_seqUrl = ConfigurationManager.Settings.SeqSinkUrl;

    public static ILogger Log { get; } = new LoggerConfiguration()
        .MinimumLevel.ControlledBy(new LoggingLevelSwitch { MinimumLevel = GetLogLevel(s_logLevel) })
        .Enrich.FromLogContext()
        .Enrich.WithThreadId()
        .Enrich.WithThreadName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .WriteTo.Console(outputTemplate: s_logFormat)
        .WriteTo.File(s_path, rollingInterval: RollingInterval.Day)
        .WriteTo.Seq(s_seqUrl)
        .CreateLogger();

    /// <summary>
    /// Returns an array of objects that can be used to format a log message.
    /// </summary>
    /// <param name="args">The objects to include in the log message.</param>
    /// <returns>An array of objects that can be used to format a log message.</returns>
    public static object[] Args(params object[] args) {
        return args;
    }

    /// <summary>
    /// Logs a verbose message with optional arguments, along with the calling class, method, and line number.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to include in the message.</param>
    /// <param name="callingClass">The name of the calling class (automatically populated by the compiler).</param>
    /// <param name="callingMethod">The name of the calling method (automatically populated by the compiler).</param>
    /// <param name="lineNumber">The line number of the calling method (automatically populated by the compiler).</param>
    public static void Verbose(string message,
                               object[]? args = null,
                               [CallerFilePath] string callingClass = "",
                               [CallerMemberName] string callingMethod = "",
                               [CallerLineNumber] int lineNumber = 0) {
        WriteLog(callingClass, callingMethod, lineNumber, LogEventLevel.Verbose, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Logs a debug message with the specified message and optional arguments.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to include in the log message.</param>
    /// <param name="callingClass">The name of the class that called this method (automatically populated by the compiler).</param>
    /// <param name="callingMethod">The name of the method that called this method (automatically populated by the compiler).</param>
    /// <param name="lineNumber">The line number in the source code where this method was called (automatically populated by the compiler).</param>
    public static void Debug(string message,
                              object[]? args = null,
                              [CallerFilePath] string callingClass = "",
                              [CallerMemberName] string callingMethod = "",
                              [CallerLineNumber] int lineNumber = 0) {
        WriteLog(callingClass, callingMethod, lineNumber, LogEventLevel.Debug, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Logs an information message with the specified message and optional arguments.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to include in the log message.</param>
    /// <param name="callingClass">The name of the class that called the logger method (automatically populated by the compiler).</param>
    /// <param name="callingMethod">The name of the method that called the logger method (automatically populated by the compiler).</param>
    /// <param name="lineNumber">The line number in the source code where the logger method was called (automatically populated by the compiler).</param>
    public static void Information(string message,
                                   object[]? args = null,
                                   [CallerFilePath] string callingClass = "",
                                   [CallerMemberName] string callingMethod = "",
                                   [CallerLineNumber] int lineNumber = 0) {
        WriteLog(callingClass, callingMethod, lineNumber, LogEventLevel.Information, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Logs a warning message along with the calling class, method and line number.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    /// <param name="args">Optional arguments to format the warning message.</param>
    /// <param name="callingClass">The calling class name (automatically populated by the compiler).</param>
    /// <param name="callingMethod">The calling method name (automatically populated by the compiler).</param>
    /// <param name="lineNumber">The line number where the warning was logged (automatically populated by the compiler).</param>
    public static void Warning(string message,
                               object[]? args = null,
                               [CallerFilePath] string callingClass = "",
                               [CallerMemberName] string callingMethod = "",
                               [CallerLineNumber] int lineNumber = 0) {
        WriteLog(callingClass, callingMethod, lineNumber, LogEventLevel.Warning, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Logs an error message along with the calling class, method and line number.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    /// <param name="args">Optional arguments to format the error message.</param>
    /// <param name="callingClass">The name of the calling class (automatically populated by the compiler).</param>
    /// <param name="callingMethod">The name of the calling method (automatically populated by the compiler).</param>
    /// <param name="lineNumber">The line number of the calling method (automatically populated by the compiler).</param>
    public static void Error(string message,
                             object[]? args = null,
                             [CallerFilePath] string callingClass = "",
                             [CallerMemberName] string callingMethod = "",
                             [CallerLineNumber] int lineNumber = 0) {
        WriteLog(callingClass, callingMethod, lineNumber, LogEventLevel.Error, message, args ?? Array.Empty<object>());
    }

    /// <summary>
    /// Logs a message with the Fatal log level.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="args">Optional arguments to format the message.</param>
    /// <param name="callingClass">The name of the class that called this method. This parameter is optional and is automatically populated by the compiler.</param>
    /// <param name="callingMethod">The name of the method that called this method. This parameter is optional and is automatically populated by the compiler.</param>
    /// <param name="lineNumber">The line number in the source code file where this method was called. This parameter is optional and is automatically populated by the compiler.</param>
    public static void Fatal(string message,
                             object[]? args = null,
                             [CallerFilePath] string callingClass = "",
                             [CallerMemberName] string callingMethod = "",
                             [CallerLineNumber] int lineNumber = 0) {
        WriteLog(callingClass, callingMethod, lineNumber, LogEventLevel.Fatal, message, args ?? Array.Empty<object>());
    }

    private static void WriteLog(string callingClass,
                                 string callingMethod,
                                 int lineNumber,
                                 LogEventLevel logLevel,
                                 string message,
                                 params object[] values) {
        // Trim the calling class to just the class name.
        callingClass = TrimCallingClass(callingClass);
        var callingSpace = $"{callingClass}.{callingMethod}@L{lineNumber}";

        // If the calling space is too long, trim it.
        callingSpace = GetConsistentSpacedName(callingSpace);

        // Push the calling space to the log context.
        LogContext.PushProperty("CallingSpace", callingSpace);

        Log.Write(logLevel, message, values);
    }

    private static LogEventLevel GetLogLevel(string logLevelString) {
        // Clean the log level string.
        logLevelString = logLevelString.Trim();
        logLevelString = logLevelString.Replace("\"", string.Empty);
        return logLevelString.ToUpper() switch {
            "TRACE" => LogEventLevel.Verbose,
            "DEBUG" => LogEventLevel.Debug,
            "INFO" => LogEventLevel.Information,
            "WARNING" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "FATAL" => LogEventLevel.Fatal,
            _ => throw new Exception($"Invalid log level: {logLevelString}"),
        };
    }

    private static string TrimCallingClass(string filePath) {
        // If the file path doesn't contain a '.cs', then it's not a C# file.
        if (!filePath.Contains(".cs")) {
            return filePath;
        }

        // Scope to the area between the final directory separator character and the '.cs' extension.
        var separatorChar = Path.DirectorySeparatorChar;
        var startIndex = filePath.LastIndexOf(separatorChar) + 1;
        var length = filePath.LastIndexOf(".cs", StringComparison.Ordinal) - startIndex;
        return filePath.Substring(startIndex, length);
    }

    private static string GetConsistentSpacedName(string name) {
        if (!string.IsNullOrEmpty(name)) {
            return name.Length > MaxCallerNameLength
                ? name[..MaxCallerNameLength]
                : name.PadRight(MaxCallerNameLength);
        }

        return "Main".PadRight(MaxCallerNameLength);
    }
}
