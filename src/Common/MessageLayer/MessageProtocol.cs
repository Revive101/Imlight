/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.Common.MessageLayer;

public abstract class MessageProtocol {
    public abstract byte ServiceId { get; }
    public abstract string ProtocolType { get; }
    public abstract string ProtocolDescription { get; }
    public abstract int ProtocolVersion { get; }

    protected Dictionary<byte, Type> Messages { get; }

    // ctor
    public MessageProtocol() {
        Messages = new Dictionary<byte, Type>();

        // Get all the sub classes of this protocol that implement IMessage.
        var concreteType = this.GetType();
        var nestedTypes = concreteType
            .GetNestedTypes()
            .Where(type => typeof(IMessage).IsAssignableFrom(type));

        // Add all the message types to the dictionary.
        foreach (var messageType in nestedTypes) {
            if (Activator.CreateInstance(messageType) is not IMessage message) {
                throw new Exception($"Failed to create instance of message type {messageType.Name}.");
            }

            // if the messages already contains the message id, skip.
            if (Messages.ContainsKey(message.MessageOrder)) {
                continue;
            }

            Messages.Add(message.MessageOrder, messageType);
        }
    }

    /// <summary>
    /// Dispatches a message based on the given message ID.
    /// </summary>
    /// <param name="messageId">The ID of the message to dispatch.</param>
    /// <returns>The dispatched message.</returns>
    public IMessage Dispatch(byte messageId) {
        if (Messages.TryGetValue(messageId, out var messageType)) {
            return Activator.CreateInstance(messageType) as IMessage
                ?? throw new Exception($"Failed to create instance of message type {messageType.Name}.");
        }
        else {
            throw new Exception($"No message found for message id {messageId}.");
        }
    }
}
