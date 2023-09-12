/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.Common.Serializable.ObjectProperty;

[AttributeUsage(AttributeTargets.Field)]
public class PropertyAttribute : Attribute
{

    public uint Hash;
    public int Flags;

    public PropertyAttribute(uint hash, int flags)
    {
        this.Hash = hash;
        this.Flags = flags;
    }
}