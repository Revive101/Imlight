using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

/*
Common
Common is not a node. It's instead a series of utilities of common functionalities
useful for all nodes.

For better elaboration, see the Common diagram:
https://app.diagrams.net/#G17utqstWzrlxPp8cVjTZX4e_Hhy8ThKSn
*/

namespace Imlight.Common.Logger
{
    public enum LogLevel
    {
        Verbose,
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// A static class to print messages to a console.
    /// </summary>
    public static class Log
    {

        //@todo: Move this to a configuration file.
        public static bool LogToFile { get; set; } = true;
        public static string LogFileName { get; set; } = "debug-log";
        public static float LogToFileTimeout { get; set; } = 5.0f;
        public static LogLevel MinimumLogLevel { get; set; } = LogLevel.Verbose;

        /// <summary>
        /// Prints a verbose log to the console.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="callerName">The original calling class.</param>
        /// <param name="lineNumber">The line message this method was called from.</param>
        /// <exception cref="ArgumentException">The message content cannot be null nor empty.</exception>
        /// <exception cref="ArgumentNullException">The callerName cannot be null nor empty.</exception>
        public static void Verbose(string message, [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentException($"'{nameof(message)}' cannot be null or empty.", nameof(message));
            if (callerName is null)            throw new ArgumentNullException(nameof(callerName));

            IrrelevantLog(LogLevel.Verbose, message, $"{callerName}:L{lineNumber}");
        }

        /// <summary>
        /// Prints a debug log to the console.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="callerName">The original calling class.</param>
        /// <param name="lineNumber">The line message this method was called from.</param>
        /// <exception cref="ArgumentException">The message content cannot be null nor empty.</exception>
        /// <exception cref="ArgumentNullException">The callerName cannot be null nor empty.</exception>
        public static void Debug(string message, [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentException($"'{nameof(message)}' cannot be null or empty.", nameof(message));
            if (callerName is null)            throw new ArgumentNullException(nameof(callerName));

            IrrelevantLog(LogLevel.Debug, message, $"{callerName}:L{lineNumber}");
        }

        /// <summary>
        /// Prints an information log to the console.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="callerName">The original calling class.</param>
        /// <param name="lineNumber">The line message this method was called from.</param>
        /// <exception cref="ArgumentException">The message content cannot be null nor empty.</exception>
        /// <exception cref="ArgumentNullException">The callerName cannot be null nor empty.</exception>
        public static void Info(string message, [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentException($"'{nameof(message)}' cannot be null or empty.", nameof(message));
            if (callerName is null)            throw new ArgumentNullException(nameof(callerName));

            GenericLog(LogLevel.Information, message, $"{callerName}:L{lineNumber}");
        }

        /// <summary>
        /// Prints a warning to the console.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="callerName">The original calling class.</param>
        /// <param name="lineNumber">The line message this method was called from.</param>
        /// <exception cref="ArgumentException">The message content cannot be null nor empty.</exception>
        /// <exception cref="ArgumentNullException">The callerName cannot be null nor empty.</exception>
        public static void Warn(string message, [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentException($"'{nameof(message)}' cannot be null or empty.", nameof(message));
            if (callerName is null)            throw new ArgumentNullException(nameof(callerName));

            GenericLog(LogLevel.Warning, message, $"{callerName}:L{lineNumber}");
        }

        /// <summary>
        /// Prints an error to the console.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="callerName">The original calling class.</param>
        /// <param name="lineNumber">The line message this method was called from.</param>
        /// <exception cref="ArgumentException">The message content cannot be null nor empty.</exception>
        /// <exception cref="ArgumentNullException">The callerName cannot be null nor empty.</exception>
        public static void Error(string message, [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentException($"'{nameof(message)}' cannot be null or empty.", nameof(message));
            if (callerName is null)            throw new ArgumentNullException(nameof(callerName));

            GenericLog(LogLevel.Error, message, $"{callerName}:L{lineNumber}");
        }

        /// <summary>
        /// Prints a fatal error to the console.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="callerName">The original calling class.</param>
        /// <param name="lineNumber">The line message this method was called from.</param>
        /// <exception cref="ArgumentException">The message content cannot be null nor empty.</exception>
        /// <exception cref="ArgumentNullException">The callerName cannot be null nor empty.</exception>
        public static void Fatal(string message, [CallerMemberName] string callerName = "", [CallerLineNumber] int lineNumber = 0)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentException($"'{nameof(message)}' cannot be null or empty.", nameof(message));
            if (callerName is null)            throw new ArgumentNullException(nameof(callerName));

            GenericLog(LogLevel.Fatal, message, $"{callerName}:L{lineNumber}");
        }

        /// <summary>
        /// Prints a test log in all log levels. 
        /// </summary>
        public static void TestLogLevels()
        {
            Verbose("This is what verbose looks like!");
            Debug("This is what debug looks like!");
            Info("This is what info looks like!");
            Warn("This is what warn looks like!");
            Error("This is what error looks like!");
            Fatal("This is what fatal looks like!");
        }

        /// <summary>
        /// Writes an "irrelevant" log for verbose or debug logs. Logs printed using this method will be entirely in a dark gray color.
        /// </summary>
        /// <param name="level">The log level of this message.</param>
        /// <param name="message">The message content.</param>
        /// <param name="callerFormat">The format of the calling method information.</param>
        /// <exception cref="LogUnsupportedInternalException"></exception>
        private static void IrrelevantLog(LogLevel level, string message, string callerFormat)
        {
            // Build log
            StringBuilder sb = new StringBuilder();

            // Add time and caller name
            string _timeFormatted = DateTime.Now.ToString("s");
            sb.Append($"[{_timeFormatted}][{callerFormat}][");

            // Write log prefix
            switch (level)
            {
                case LogLevel.Verbose:
                    sb.Append($"VERB]: {message}{Environment.NewLine}");
                    break;
                case LogLevel.Debug:
                    sb.Append($"DEBG]: {message}{Environment.NewLine}");
                    break;
                default:
                    throw new LogUnsupportedInternalException($"Log level of \"{level}\" is unsupported with this method!" +
                        $"Use GenericLog() instead.");
            }

            WriteColored(sb.ToString(), ConsoleColor.DarkGray);
        }

        /// <summary>
        /// Writes a generic log. Does not support verbose or debug logs. Use IrrelevantLog() instead.
        /// </summary>
        /// <param name="level">The log level of this message.</param>
        /// <param name="message">The message content.</param>
        /// <param name="callerFormat">The format of the calling method information.</param>
        /// <exception cref="LogUnsupportedInternalException">This does not support verbose or debug logs. Use IrrelevantLog() instead.</exception>
        private static void GenericLog(LogLevel level, string message, string callerFormat)
        {
            // Build log
            // $"[${DateTime.Now}][{callerName}][VERBO]: {message}";

            // Write time and caller name
            string _timeFormatted = DateTime.Now.ToString("s");
            Console.Write($"[{_timeFormatted}][{callerFormat}][");

            // Write colored log prefix
            switch (level)
            {
                case LogLevel.Verbose:
                case LogLevel.Debug:
                    Error($"LogLevel {level} is not supported by the GenericLog function. Please use LogIrrelevant.");
                    return;
                case LogLevel.Information:
                    WriteColored("INFO", ConsoleColor.White);
                    break;
                case LogLevel.Warning:
                    WriteColored("WARN", ConsoleColor.DarkYellow);
                    break;
                case LogLevel.Error:
                    WriteColored("ERRO", ConsoleColor.Red);
                    break;
                case LogLevel.Fatal:
                    WriteColored("FATL", ConsoleColor.White, ConsoleColor.Red);
                    break;
                default:
                    throw new LogUnsupportedInternalException($"Log level of \"{level}\" is unsupported with this method!" +
                        $"Use IrrelevantLog() instead.");
            }

            // Write message+
            Console.Write($"]: {message}{Environment.NewLine}");

            // Log to local file
            if (LogToFile)
            {
                // Rebuild message
                string fullMessage = $"[{_timeFormatted}][{callerFormat}][{level}]: {message}\n";
                LocalFileWriter.WriteToLogFile(fullMessage);
            }
        }

        /// <summary>
        /// Writes a statement to the log in a given color.
        /// </summary>
        /// <param name="message">The message content.</param>
        /// <param name="color">The foreground color of the log.</param>
        /// <param name="bgColor">The background color of the log.</param>
        /// <exception cref="ArgumentException">The message content cannot be null nor empty.</exception>
        private static void WriteColored(string message, ConsoleColor color, ConsoleColor bgColor = ConsoleColor.Black)
        {
            if (string.IsNullOrEmpty(message)) throw new ArgumentException($"'{nameof(message)}' cannot be null or empty.", nameof(message));

            ConsoleColor _defaultFg = Console.ForegroundColor;
            ConsoleColor _defaultBg = Console.BackgroundColor;

            // Set colors
            Console.ForegroundColor = color;
            Console.BackgroundColor = bgColor;

            Console.Write(message);

            // Set colors back to default
            Console.ForegroundColor = _defaultFg;
            Console.BackgroundColor = _defaultBg;
        }

    }
}
