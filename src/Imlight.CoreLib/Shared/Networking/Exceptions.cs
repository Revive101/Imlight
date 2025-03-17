/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Runtime.Serialization;

namespace Imlight.CoreLib.Shared.Networking;

public class SessionFatalException : Exception {
    public string CallingClass { get; private set; }
    public int LineNumber { get; private set; }

    public SessionFatalException() { }

    public SessionFatalException(string message)
        : base(message) {
        InitializeCallingMethod();
    }

    public SessionFatalException(string message, Exception inner)
        : base(message, inner) {
        InitializeCallingMethod();
    }

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

    protected SessionFatalException(SerializationInfo info, StreamingContext context)
        : base(info, context) {
        CallingClass = info.GetString("CallingClass");
        LineNumber = info.GetInt32("LineNumber");
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
        base.GetObjectData(info, context);
        info.AddValue("CallingClass", CallingClass);
        info.AddValue("LineNumber", LineNumber);
    }

    private void InitializeCallingMethod() {
        var stackTrace = new System.Diagnostics.StackTrace(this, true);
        var frame = stackTrace.GetFrame(1); // Get the caller's frame (1 frame above the current one).
        if (frame != null) {
            CallingClass = frame.GetMethod().DeclaringType.FullName;
            LineNumber = frame.GetFileLineNumber();
        }
    }
}

public class ServiceRetryException : Exception {
    public string CallingClass { get; private set; }
    public int LineNumber { get; private set; }

    public ServiceRetryException() { }

    public ServiceRetryException(string message)
        : base(message) {
        InitializeCallingMethod();
    }

    public ServiceRetryException(string message, Exception inner)
        : base(message, inner) {
        InitializeCallingMethod();
    }

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

    protected ServiceRetryException(SerializationInfo info, StreamingContext context)
        : base(info, context) {
        CallingClass = info.GetString("CallingClass");
        LineNumber = info.GetInt32("LineNumber");
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context) {
        base.GetObjectData(info, context);
        info.AddValue("CallingClass", CallingClass);
        info.AddValue("LineNumber", LineNumber);
    }

    private void InitializeCallingMethod() {
        var stackTrace = new System.Diagnostics.StackTrace(this, true);
        var frame = stackTrace.GetFrame(1); // Get the caller's frame (1 frame above the current one).
        if (frame != null) {
            CallingClass = frame.GetMethod().DeclaringType.FullName;
            LineNumber = frame.GetFileLineNumber();
        }
    }
}
