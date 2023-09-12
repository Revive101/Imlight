/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Imlight.Common.DML;
using Imlight.Common.IO;
using Imlight.Common.Utilities;

namespace Imlight.Common.Serializable;

public static class MessageSerializer
{
    private static readonly IReadOnlyDictionary<DmlType, byte> DmlTypeSize = new Dictionary<DmlType, byte>()
    {
        { DmlType.BYT, 1 },
        { DmlType.BOOL, 1 },
        { DmlType.UBYT, 1 },
        { DmlType.SHRT, 2 },
        { DmlType.USHRT, 2 },
        { DmlType.INT, 4 },
        { DmlType.UINT, 4 },
        { DmlType.STR, 2 },
        { DmlType.WSTR, 2 },
        { DmlType.FLT, 4 },
        { DmlType.DBL, 8 },
        { DmlType.GID, 8 }
    };
    private static readonly IReadOnlyDictionary<DmlType, Func<BitIterator, object>> DmlReaders
        = new Dictionary<DmlType, Func<BitIterator, object>>()
        {
            { DmlType.BYT,   (r)   => r.ReadInt8()    },
            { DmlType.BOOL,  (r)   => r.ReadBool()    },
            { DmlType.UBYT,  (r)   => r.ReadUInt8()   },
            { DmlType.SHRT,  (r)   => r.ReadInt16()   },
            { DmlType.USHRT, (r)   => r.ReadUInt16()  },
            { DmlType.INT,   (r)   => r.ReadInt32()   },
            { DmlType.UINT,  (r)   => r.ReadUInt32()  },
            { DmlType.STR,   (r)   => r.ReadString()  },
            { DmlType.WSTR,  (r)   => new WideByteString(r.ReadWString()) },
            { DmlType.FLT,   (r)   => r.ReadFloat()   },
            { DmlType.DBL,   (r)   => r.ReadDouble()  },
            { DmlType.GID,   (r)   => r.ReadUInt64()  },
        };
    private static readonly IReadOnlyDictionary<DmlType, Action<BitIterator, object>> DmlWriters
        = new Dictionary<DmlType, Action<BitIterator, object>>()
        {
            { DmlType.BYT,   (r, v)   => r.WriteInt8((sbyte)v)        },
            { DmlType.BOOL,  (r, v)   => r.WriteUInt8((byte)v)        },
            { DmlType.UBYT,  (r, v)   => r.WriteUInt8((byte)v)        },
            { DmlType.SHRT,  (r, v)   => r.WriteInt16((short)v)       },
            { DmlType.USHRT, (r, v)   => r.WriteUInt16((ushort)v)     },
            { DmlType.INT,   (r, v)   => r.WriteInt32((int)v)         },
            { DmlType.UINT,  (r, v)   => r.WriteUInt32((uint)v)       },
            { DmlType.STR,   (r, v)   => r.WriteString((ByteString)v) },
            { DmlType.WSTR,  (r, v)   => r.WriteWString((WideByteString)v)},
            { DmlType.FLT,   (r, v)   => r.WriteFloat((float)v)       },
            { DmlType.DBL,   (r, v)   => r.WriteDouble((double)v)     },
            { DmlType.GID,   (r, v)   => r.WriteUInt64((ulong)v)      },
        };

    private const ushort MagicHeader = (ushort)0xF00Du;

    /// <summary>
    /// Serialize a INetworkMessage class into a binary stream to send to a Wizard101 client.
    /// </summary>
    /// <param name="message">The <see cref="INetworkMessage"/> message object. </param>
    public static byte[] SerializeMessageBinary(INetworkMessage message)
    {
        // Use reflection to get each field that contains the DML attribute.
        var elements = message
            .GetType()
            .GetFields()
            .Where(f => f.IsDefined(typeof(DmlElementAttribute), false));

        // Iterate through the DML fields and use the corresponding writer to add to a binary buffer.
        var writer = new BitIterator();
        foreach (var element in elements)
        {
            var dmlType = GetDmlTypeFromAttr(element);
            DmlWriters[dmlType].Invoke(writer, element.GetValue(message));
        }

        // Now that all the elements have been written to a buffer, we can craft the header.
        var bytes = writer.GetData();
        using var headerWriter = new BitIterator();
        headerWriter.WriteUInt16(MagicHeader);

        // Write large header or small header, depending on the size of the payload.
        // The +9 comes from the body header itself plus the trailing null byte.
        var isLongPacket = bytes.Length > 0x777F;
        var isControl = message.ServiceId == 0;
        var smallPacketLength = isControl ? bytes.Length + 5 : bytes.Length + 9;
        headerWriter.WriteUInt16((ushort)(isLongPacket ? 0x8000 : smallPacketLength));
        if (isLongPacket)
        {
            headerWriter.WriteUInt32((uint)bytes.Length);
        }

        // Write body header
        headerWriter.WriteUInt8((byte)(isControl ? 1 : 0));                    // isControl
        headerWriter.WriteUInt8((byte)(isControl ? message.MessageOrder : 0)); // opCode
        headerWriter.WriteUInt16(0);                                      // Padding
        var headerBytes = headerWriter.GetData();
        if (isControl)
        {
            // If this is a control message, we can simply craft the whole packet and return;
            // Create a new byte array from all the buffers combined. The +1 is the trailing null byte.
            var packet = new byte[bytes.Length + headerBytes.Length + 1];

            // Combine all the buffers into the new byte array we just created.
            headerBytes.CopyTo(packet, 0);
            bytes.CopyTo(packet, headerBytes.Length);

            return packet;
        }
        else
        {
            // However, DML messages require a secondary header to be built.
            // Craft that header here.
            var dmlHeaderWriter = new BitIterator();
            dmlHeaderWriter.WriteUInt8(message.ServiceId);
            dmlHeaderWriter.WriteUInt8(message.MessageOrder);
                
            // Write the length of the following body plus this header.
            dmlHeaderWriter.WriteUInt16((ushort)(bytes.Length + 4));

            var dmlHeaderBytes = dmlHeaderWriter.GetData();

            // Create a new byte array from all the buffers combined. The +1 is the trailing null byte.
            var packet = new byte[bytes.Length + dmlHeaderBytes.Length + headerBytes.Length + 1];

            // Combine all the buffers into the new byte array we just created.
            headerBytes.CopyTo(packet, 0);
            dmlHeaderBytes.CopyTo(packet, headerBytes.Length);
            bytes.CopyTo(packet, headerBytes.Length + dmlHeaderBytes.Length);

            return packet;
        }
    }

    /// <summary>
    /// Deserializes an incoming Wizard101 packet to a INetworkObject.
    /// </summary>
    /// <param name="binaryBuffer">The binary buffer from a packet.</param>
    /// <returns>An <see cref="INetworkMessage"/> object.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static INetworkMessage DeserializeMessageBinary(byte[] binaryBuffer)
    {
        if (binaryBuffer is null) throw new ArgumentNullException(nameof(binaryBuffer));
        if (binaryBuffer.Length <= 0) throw new ArgumentOutOfRangeException(nameof(binaryBuffer));

        // Create binary reader.
        var stream = new MemoryStream(binaryBuffer);
        var reader = new BitIterator(stream);
            
        var header = reader.ReadUInt16();
        if (header != MagicHeader)
            throw new ArgumentException($"The given binary buffer did not contain the magic header!");
        var length = reader.ReadUInt16();

        var isControl = reader.ReadBool();
        var opCode = reader.ReadUInt8();
        reader.ReadUInt16(); // Padding bytes.

        // If this is not a control message, it means it's a data message.
        // Data messages have a secondary header to indicate service ID and message ID.
        var svcid = (isControl ? (byte)0 : reader.ReadUInt8());
        var msgid = (isControl ? opCode : reader.ReadUInt8());
        if (!isControl)
            reader.ReadUInt16(); // Read DML length

        // Dispatch to the corresponding protocol and find message record.
        var protocol = ProtocolDispatcher.Dispatch(svcid);
        var message = protocol?.Dispatch(msgid);
        if (protocol is null || message is null) return null;

        // The binary buffer will be in order of the DML elements as they are written.
        // We can use Reflection to map these variables accordingly.
        var recordFields = message
            .GetType()
            .GetFields()
            .Where(f => f.IsDefined(typeof(DmlElementAttribute), false));

        // First we'll check to see if the record we dispatched is valid.
        // Simply check how many fields there are in the record and compare them to the bytes we still have left.
        // The reason we don't use the DML payload length is to let this be ambiguous between DML and control messages.
        var bytesLeft = reader.Count() - reader.Count();
        if (!DoesRecordFitBounds(bytesLeft, message))
            throw new InvalidOperationException("Incorrect record! " +
                                                "Record fields does not fit in the bounds of the binary buffer.");

        // Iterate through the record fields and read the appropriate type from the binary buffer.
        foreach (var field in recordFields)
        {
            var dmlType = GetDmlTypeFromAttr(field);
            var val = DmlReaders[dmlType](reader);
            field.SetValue(message, val);
        }

        return message;
    }

    private static bool DoesRecordFitBounds(long byteCount, INetworkMessage message)
    {
        var messageFields = GetAttributesFromType<DmlElementAttribute>(message);
        var currentCount = 
            messageFields.Aggregate(0, (current, field) => current + GetDmlTypeSize(field.SerializedType));

        return currentCount <= byteCount;
    } 

    private static DmlType GetDmlTypeFromAttr(FieldInfo field)
    {
        // Get the DMLType from the attribute.
        var dmlElements = field.GetCustomAttributes(typeof(DmlElementAttribute), false)
            .Cast<DmlElementAttribute>()
            .ToArray();
        switch (dmlElements.Count())
        {
            case 0:
                Log.Logger.Error($"Attempted to get DMLField attribute from [{field.Name}], " +
                                 $"but it did not contain one. Returning 0 size.");
                return 0;
            case > 1:
                Log.Logger.Error($"DMLField [{field.Name}] contained duplicate attributes!");
                break;
        }

        return dmlElements[0].SerializedType;
    }

    private static byte GetDmlTypeSize(DmlType type)
    {
        if (!DmlTypeSize.TryGetValue(type, out var size))
            throw new Exception($"Could not get size for DMLType [{type.ToString()}].");

        return size;
    }
        
    private static IEnumerable<T> GetAttributesFromType<T>(object type) where T : Attribute
    {
        return type.GetType().GetProperties()
            .Where(f => f.IsDefined(typeof(T), false))
            .Cast<T>();
    }

}