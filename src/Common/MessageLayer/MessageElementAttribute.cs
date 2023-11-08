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
