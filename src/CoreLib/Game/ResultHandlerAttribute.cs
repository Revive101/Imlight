/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.CoreLib.Game;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ResultHandlerAttribute : Attribute {
    public Type ResultType { get; }

    public ResultHandlerAttribute(Type resultType) {
        this.ResultType = resultType;
    }
}
