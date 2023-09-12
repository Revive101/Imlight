/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.Common.DML;

[System.AttributeUsage(System.AttributeTargets.Field |
                       System.AttributeTargets.Property)]
public class DmlElementAttribute : System.Attribute
{
    public readonly DmlType SerializedType;

    public DmlElementAttribute(DmlType serializedType)
    {
        SerializedType = serializedType;
    }
}