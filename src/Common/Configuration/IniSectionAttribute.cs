using System;

namespace Imlight.Common.Configuration;

[AttributeUsage(AttributeTargets.All)]
public class IniSectionAttribute : Attribute {
    public string SectionName { get; }

    public IniSectionAttribute(string sectionName) {
        SectionName = sectionName;
    }
}
