/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.Common.MessageLayer;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class MessageElementAttribute : Attribute {
    public readonly string SerializedType;

    // ctor
    public MessageElementAttribute(string serializedType) {
        SerializedType = serializedType;
    }
}
