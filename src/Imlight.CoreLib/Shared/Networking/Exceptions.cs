/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Imlight.CoreLib.Shared.Networking;

// Modern approach using System.Text.Json for serialization
public class SessionFatalException : Exception {
    
    public string CallingClass { get; private set; }
    public int LineNumber { get; private set; }

    public SessionFatalException() { }

    public SessionFatalException(string message)
        : base(message) => InitializeCallingMethod();

    public SessionFatalException(string message, Exception inner)
        : base(message, inner) => InitializeCallingMethod();

    public SessionFatalException(string message, string callingClass, int lineNumber)
        : base(message) {
        CallingClass = callingClass;
        LineNumber = lineNumber;
    }

    public SessionFatalException(string message, Exception inner, string callingClass, int lineNumber)
        : base(message, inner) {
        CallingClass = callingClass;
        LineNumber = lineNumber;
    }

    // Modern approach to getting caller information
    private void InitializeCallingMethod([CallerMemberName] string memberName = "",
                                        [CallerFilePath] string sourceFilePath = "",
                                        [CallerLineNumber] int sourceLineNumber = 0) {
        // For more precise control, still use StackTrace
        var stackTrace = new StackTrace(true);
        var frame = stackTrace.GetFrame(1); // Get the caller's frame

        if (frame != null) {
            var method = frame.GetMethod();
            if (method?.DeclaringType != null) {
                CallingClass = method.DeclaringType.FullName;
                LineNumber = frame.GetFileLineNumber();
            }
            else {
                // Fallback to CallerAttributes if StackTrace doesn't provide enough info
                CallingClass = sourceFilePath;
                LineNumber = sourceLineNumber;
            }
        }
    }

}

public class ServiceRetryException : Exception {

    public string CallingClass { get; private set; }
    public int LineNumber { get; private set; }

    public ServiceRetryException() { }

    public ServiceRetryException(string message)
        : base(message) => InitializeCallingMethod();

    public ServiceRetryException(string message, Exception inner)
        : base(message, inner) => InitializeCallingMethod();

    public ServiceRetryException(string message, string callingClass, int lineNumber)
        : base(message) {
        CallingClass = callingClass;
        LineNumber = lineNumber;
    }

    public ServiceRetryException(string message, Exception inner, string callingClass, int lineNumber)
        : base(message, inner) {
        CallingClass = callingClass;
        LineNumber = lineNumber;
    }

    // Modern approach to getting caller information
    private void InitializeCallingMethod([CallerMemberName] string memberName = "",
                                        [CallerFilePath] string sourceFilePath = "",
                                        [CallerLineNumber] int sourceLineNumber = 0) {
        // For more precise control, still use StackTrace
        var stackTrace = new StackTrace(true);
        var frame = stackTrace.GetFrame(1); // Get the caller's frame

        if (frame != null) {
            var method = frame.GetMethod();
            if (method?.DeclaringType != null) {
                CallingClass = method.DeclaringType.FullName;
                LineNumber = frame.GetFileLineNumber();
            }
            else {
                // Fallback to CallerAttributes if StackTrace doesn't provide enough info
                CallingClass = sourceFilePath;
                LineNumber = sourceLineNumber;
            }
        }
    }

}