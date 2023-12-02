/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imlight.Common.IO;

namespace Imlight.Common.MessageLayer;

public static class MessageSerializer {
    private const ushort MagicHeader = (ushort) 0xF00Du;
    private const byte ControlMessageServiceId = 0;
    private const byte PacketHeaderLengthSmall = 5; // +1 from the trailing null byte
    private const byte PacketHeaderLengthLarge = 9; // +1 from the trailing null byte
    private const byte MessageLayerHeaderLength = 4;

    /// <summary>
    /// Encodes the given IMessage object into a byte array.
    /// </summary>
    /// <param name="message">The IMessage object to encode.</param>
    /// <returns>A byte array representing the encoded message.</returns>
    public static byte[] Encode(IMessage message) {
        var bodyData = EncodePacketBody(message);

        // Begin work on encoding a packet frame.
        var isControl = message.ServiceId == 0;
        var headerData = EncodePacketHeader(bodyData.Length, isControl, message.MessageOrder);

        if (isControl) {
            // If this is a control message, we can simply craft the whole packet and return;
            var packet = EncodeFullPacket(headerData, bodyData);

            return packet;
        }
        else {
            // However, DML messages require a secondary header to be built. Craft that header here.
            var secondaryHeaderData = EncodeMessageLayerHeader(message.ServiceId, message.MessageOrder, bodyData.Length);
            var fullHeaderData = new byte[headerData.Length + secondaryHeaderData.Length];

            // Write the header data to the full header buffer.
            headerData.CopyTo(fullHeaderData, 0);
            secondaryHeaderData.CopyTo(fullHeaderData, headerData.Length);

            var packet = EncodeFullPacket(fullHeaderData, bodyData);

            return packet;
        }
    }

    /// <summary>
    /// Decodes a byte array into a collection of messages.
    /// </summary>
    /// <param name="buffer">The byte array to decode.</param>
    /// <returns>A collection of messages if the decoding is successful, otherwise null.</returns>
    public static IReadOnlyCollection<IMessage>? Decode(byte[] buffer) {
        if (buffer.Length <= 0) {
            Logger.Error("Attempted to decode a message with no data!");
            return null;
        }

        // Create a new BitReader from the buffer. To start, we'll read the first 4 bytes, which should be a magic header.
        // If this is not a magic packet, we won't bother decoding it.
        var reader = new BitReader(buffer);
        var isMagicPacket = DecodeMagicHeader(reader);
        if (!isMagicPacket) {
            Logger.Error("Attempted to decode a message with an invalid magic header!");
            return null;
        }

        var (packetLength, isControl, opCode) = DecodePacketHeader(reader);

        // Can remove this section later. It's just here to see if it happens

        if (packetLength > buffer.Length) {
            Logger.Information("Hey look at that! We found evidence of a packet being squashed!");
        }

        // =====================

        // Control messages are never squashed. Decode the entire packet as one control message and return.
        if (isControl) {
            var controlMessageTemplate = MessageDispatcher.Dispatch(ControlMessageServiceId, opCode);
            var decodedControlpacket = DecodePacketBody(reader, controlMessageTemplate);
            if (decodedControlpacket is null) {
                return null;
            }

            return new IMessage[] { decodedControlpacket };
        }

        var messages = new List<IMessage>();
        while (reader.BitPos() < packetLength * 8) {
            var message = DecodeMessage(reader);
            if (message != null) {
                messages.Add(message);
            }
        }

        return messages;
    }

    private static byte[] EncodePacketHeader(int bodyDataLength, bool isControl, byte opCode) {
        using var headerWriter = new BitWriter();
        headerWriter.WriteUInt16(MagicHeader);

        // Write large header or small header, depending on the size of the payload.
        var isLongPacket = bodyDataLength > 0x777F;
        var smallPacketLength = isControl
            ? bodyDataLength + PacketHeaderLengthSmall
            : bodyDataLength + PacketHeaderLengthLarge;
        headerWriter.WriteUInt16((ushort) (isLongPacket ? 0x8000 : smallPacketLength));

        // If the packet is larger than what can fit in an unsigned 16-bit integer, we need to write the actual length
        // of the packet in the next 4 bytes.
        if (isLongPacket) {
            headerWriter.WriteUInt32((uint) bodyDataLength);
        }

        // Write body header
        headerWriter.WriteUInt8((byte) (isControl ? 1 : 0));      // isControl
        headerWriter.WriteUInt8((byte) (isControl ? opCode : 0)); // opCode
        headerWriter.WriteUInt16(0);                              // Padding

        return headerWriter.GetData();
    }

    private static byte[] EncodeMessageLayerHeader(byte serviceId, byte messageOrder, int bodyDataLength) {
        using var headerWriter = new BitWriter();
        headerWriter.WriteUInt8(serviceId);
        headerWriter.WriteUInt8(messageOrder);

        // Write the length of the body.
        headerWriter.WriteUInt16((ushort) (bodyDataLength + MessageLayerHeaderLength));

        return headerWriter.GetData();
    }

    private static byte[] EncodePacketBody(IMessage message) {
        // Use reflection to get each field that contains the MessageElementAttribute.
        var elements = message
            .GetType()
            .GetFields()
            .Where(f => f.IsDefined(typeof(MessageElementAttribute), false));

        // Iterate through the DML fields and use the corresponding writer to add to a binary buffer.
        var writer = new BitWriter();
        foreach (var element in elements) {
            var dmlType = GetDmlTypeFromAttr(element);
            var value = element.GetValue(message);

            // We may write null here. This is fine, as the writer will simply write empty bytes.
            MessageElementWriters.WriteDml(writer, dmlType, value);
        }

        return writer.GetData();
    }

    private static byte[] EncodeFullPacket(byte[] header, byte[] payload) {
        // Create a new byte array from all the buffers combined. The +1 is the trailing null byte.
        var packet = new byte[payload.Length + header.Length + 1];

        // Combine all the buffers into the new byte array we just created.
        header.CopyTo(packet, 0);
        payload.CopyTo(packet, header.Length);

        return packet;
    }

    private static bool DecodeMagicHeader(BitReader reader) {
        var header = reader.ReadUInt16();

        return header == MagicHeader;
    }

    private static (ushort, bool, byte) DecodePacketHeader(BitReader reader) {
        var packetLength = reader.ReadUInt16();
        var isControl = reader.ReadBool();
        var opCode = reader.ReadUInt8();
        _ = reader.ReadUInt16(); // Padding

        return (packetLength, isControl, opCode);
    }

    private static IMessage? DecodeMessage(BitReader reader) {
        var (serviceId, messageId, messageLength) = DecodeMessageLayerHeader(reader);

        var messageTemplate = MessageDispatcher.Dispatch(serviceId, messageId);
        if (messageTemplate is null) {
            var args = Logger.Args(serviceId, messageId);
            Logger.Error("Could not dispatch a decoded message with service id {0} and message id {1}", args);

            return null;
        }

        var message = DecodePacketBody(reader, messageTemplate);
        return message;
    }

    private static (byte, byte, ushort) DecodeMessageLayerHeader(BitReader reader) {
        var serviceId = reader.ReadUInt8();
        var messageId = reader.ReadUInt8();
        var length = (ushort) (reader.ReadUInt16() + 4);

        return (serviceId, messageId, length);
    }

    private static IMessage? DecodePacketBody(BitReader reader, IMessage message) {
        var recordFields = message
            .GetType()
            .GetFields()
            .Where(f => f.IsDefined(typeof(MessageElementAttribute), false));

        // First we'll check to see if the record we dispatched is valid.
        // Simply check how many fields there are in the record and compare them to the bytes we still have left.
        // The reason we don't use the DML payload length is to let this be ambiguous between DML and control messages.
        if (!CheckPacketLengthValidity(reader, message)) {
            Logger.Error("Attempted to decode a packet body, but failed because the given message template " +
                         "did not match the size found in the packet header.");
            return null;
        }

        // Iterate through the record fields and read the appropriate type from the binary buffer.
        foreach (var field in recordFields) {
            var dmlType = GetDmlTypeFromAttr(field);
            var val = MessageElementReader.ReadDml(reader, dmlType);
            field.SetValue(message, val);
        }

        return message;
    }

    private static bool CheckPacketLengthValidity(BitReader reader, IMessage message) {
        var bytesLeft = reader.Count();
        if (!DoesRecordFitBounds(bytesLeft, message)) {
            return false;
        }

        return true;
    }

    private static bool DoesRecordFitBounds(long byteCount, IMessage message) {
        var messageFields = GetAttributesFromType<MessageElementAttribute>(message);
        var currentCount =
            messageFields.Aggregate(0, (current, field) => current + MessageTypeSizes.GetSize(field.SerializedType));

        return currentCount <= byteCount;
    }

    private static IEnumerable<T> GetAttributesFromType<T>(object type) where T : Attribute {
        return type.GetType().GetProperties()
            .Where(f => f.IsDefined(typeof(T), false))
            .Cast<T>();
    }

    private static string GetDmlTypeFromAttr(FieldInfo field) {
        // Get the DMLType from the attribute.
        var dmlElements = field.GetCustomAttributes(typeof(MessageElementAttribute), false)
            .Cast<MessageElementAttribute>()
            .ToArray();
        switch (dmlElements.Count()) {
            case 0:
                Logger.Error($"Attempted to get DMLField attribute from {0}, but it did not contain one. Returning 0 size.",
                    Logger.Args(field.Name));
                return "";
            case > 1:
                Logger.Error($"DMLField {0} contained duplicate attributes!",
                    Logger.Args(field.Name));
                break;
        }

        return dmlElements[0].SerializedType;
    }
}
