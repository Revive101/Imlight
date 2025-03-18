/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.CoreLib.Shared.Networking;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class MessageHandlerAttribute(Type messageType) : Attribute {

    public Type MessageType { get; } = messageType;
    
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class InternalMessageHandlerAttribute(Type messageType) : MessageHandlerAttribute(messageType) { }