using System;

namespace Imlight.Common.Configuration;

[AttributeUsage(AttributeTargets.Property)]
public class DescriptionAttribute : Attribute
{
    public string Description { get; }

    public DescriptionAttribute(string description)
    {
        Description = description;
    }
}