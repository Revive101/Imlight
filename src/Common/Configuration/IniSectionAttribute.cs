/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.Common.Configuration;

[AttributeUsage(AttributeTargets.All)]
public class IniSectionAttribute : Attribute {
    public string SectionName { get; }

    public IniSectionAttribute(string sectionName) {
        SectionName = sectionName;
    }
}
