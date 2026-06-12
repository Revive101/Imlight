/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Imlight.CoreLib.Shared.Networking;

internal class SessionFatalException : Exception {
    
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

    private void InitializeCallingMethod([CallerMemberName] string memberName = "",
                                        [CallerFilePath] string sourceFilePath = "",
                                        [CallerLineNumber] int sourceLineNumber = 0) {
        var stackTrace = new StackTrace(true);
        var frame = stackTrace.GetFrame(1); // Get the caller's frame.

        if (frame != null) {
            var method = frame.GetMethod();
            if (method?.DeclaringType != null) {
                CallingClass = method.DeclaringType.FullName;
                LineNumber = frame.GetFileLineNumber();
            }
            else {
                // Fallback to CallerAttributes if StackTrace doesn't provide enough info.
                CallingClass = sourceFilePath;
                LineNumber = sourceLineNumber;
            }
        }
    }

}

internal class ServiceRetryException : Exception {

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

    private void InitializeCallingMethod([CallerMemberName] string memberName = "",
                                        [CallerFilePath] string sourceFilePath = "",
                                        [CallerLineNumber] int sourceLineNumber = 0) {
        var stackTrace = new StackTrace(true);
        var frame = stackTrace.GetFrame(1); // Get the caller's frame.

        if (frame != null) {
            var method = frame.GetMethod();
            if (method?.DeclaringType != null) {
                CallingClass = method.DeclaringType.FullName;
                LineNumber = frame.GetFileLineNumber();
            }
            else {
                // Fallback to CallerAttributes if StackTrace doesn't provide enough info.
                CallingClass = sourceFilePath;
                LineNumber = sourceLineNumber;
            }
        }
    }

}