/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.Common.Configuration;

[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
internal sealed class DefaultValueAttribute : Attribute {
    public object Value { get; }

    public DefaultValueAttribute(object value) {
        Value = value;
    }
}
