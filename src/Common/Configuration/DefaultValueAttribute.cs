using System;

namespace Imlight.Common.Configuration;

[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
internal sealed class DefaultValueAttribute : Attribute
{
    public object Value { get; }

    public DefaultValueAttribute(object value)
    {
        Value = value;
    }
}