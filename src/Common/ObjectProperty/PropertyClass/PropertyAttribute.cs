using System;

namespace Imlight.Common.ObjectProperty.PropertyReflection;

[AttributeUsage(AttributeTargets.Field)]
public class PropertyAttribute : Attribute {
    public uint Hash;
    public int Flags;

    public PropertyAttribute(uint hash, int flags) {
        this.Hash = hash;
        this.Flags = flags;
    }
}
