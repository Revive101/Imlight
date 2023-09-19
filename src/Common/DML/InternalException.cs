/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Runtime.Serialization;

namespace Imlight.Common.DML;

public class InternalException : Exception
{
    public InternalException()
    {
    }

    public InternalException(string message) : base(message)
    {
    }

    public InternalException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected InternalException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}