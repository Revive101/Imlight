/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.Common.Configuration;

[AttributeUsage(AttributeTargets.Property)]
public class DescriptionAttribute : Attribute {
    public string Description { get; }

    public DescriptionAttribute(string description) {
        Description = description;
    }
}
