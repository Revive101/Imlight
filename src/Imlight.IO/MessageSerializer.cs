using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using Imlight.Internals;
using Imlight.Internals.DML;
using Imlight.Common;

namespace Imlight.IO
{
    public static class MessageSerializer
    {

        private static readonly IReadOnlyDictionary<DMLType, byte> _dmlTypeSize = new Dictionary<DMLType, byte>()
        {
            { DMLType.BYT, 1 },
            { DMLType.UBYT, 1 },
            { DMLType.SHRT, 2 },
            { DMLType.USHRT, 2 },
            { DMLType.INT, 4 },
            { DMLType.UINT, 4 },
            { DMLType.STR, 2 },
            { DMLType.WSTR, 2 },
            { DMLType.FLT, 4 },
            { DMLType.DBL, 8 },
            { DMLType.GID, 8 }
        };
        private static readonly IReadOnlyDictionary<DMLType, Func<BitBuffer, object>> _dmlReaders
            = new Dictionary<DMLType, Func<BitBuffer, object>>()
        {
            { DMLType.BYT,   (r)   => r.ReadUInt8()      },
            { DMLType.UBYT,  (r)   => r.ReadInt8()      },
            { DMLType.SHRT,  (r)   => r.ReadInt16()       },
            { DMLType.USHRT, (r)   => r.ReadUInt16()      },
            { DMLType.INT,   (r)   => r.ReadInt32()       },
            { DMLType.UINT,  (r)   => r.ReadUInt32()      },
            { DMLType.STR,   (r)   => r.ReadString() },
            { DMLType.WSTR,  (r)   => r.ReadCString()      },
            { DMLType.FLT,   (r)   => r.ReadFloat()      },
            { DMLType.DBL,   (r)   => r.ReadDouble()      },
            { DMLType.GID,   (r)   => r.ReadUInt64()      },
        };
        private static readonly IReadOnlyDictionary<DMLType, Action<BitBuffer, object>> _dmlWriters
            = new Dictionary<DMLType, Action<BitBuffer, object>>()
        {
            { DMLType.BYT,   (r, v)   => r.WriteInt8((sbyte)v)   },
            { DMLType.UBYT,  (r, v)   => r.WriteUInt8((byte)v)     },
            { DMLType.SHRT,  (r, v)   => r.WriteInt16((short)v)   },
            { DMLType.USHRT, (r, v)   => r.WriteUInt16((ushort)v) },
            { DMLType.INT,   (r, v)   => r.WriteInt32((int)v)      },
            { DMLType.UINT,  (r, v)   => r.WriteUInt32((uint)v)    },
            { DMLType.STR,   (r, v)   => r.WriteString((string)v)   },
            { DMLType.WSTR,  (r, v)   => r.WriteString((string)v)  },
            { DMLType.FLT,   (r, v)   => r.WriteFloat((float)v)    },
            { DMLType.DBL,   (r, v)   => r.WriteDouble((double)v)   },
            { DMLType.GID,   (r, v)   => r.WriteUInt64((ulong)v)    },
        };

        /// <summary>
        /// Serialize a INetworkMessage class into a binary stream to send to a Wizard101 client.
        /// </summary>
        /// <param name="message"></param>
        public static byte[] SerializeMessageBinary(INetworkMessage message)
        {
            // For this, we'll iterate through the DML elements, writing to a binarybuffer as we go.
            // Once we have all the elements, we can craft the header.

            var elements = message.GetType().GetFields()
                .Where(f => f.IsDefined(typeof(DMLElementAttribute), false));
            var writer = new BitBuffer();

            foreach (var element in elements)
            {
                // Get DMLElement attribute from this property.
                var dmlType = GetDmlTypeFromAttr(element);

                _dmlWriters[dmlType].Invoke(writer, element.GetValue(element));
            }

            // Now that all the elements have been defined, we can craft the header.
            // Refer to docs: https://w101r.github.io/rewritten-docs/documentation/KINP/packet-framing/

            var bytes = writer.GetData();

            // Write first packet header.
            using var headerWriter = new BitBuffer();
            headerWriter.WriteUInt16(0xF00D);
            // Write large header or small header, depending on the size of the payload.
            // The +9 comes from the body header itself, and the trailing null byte.
            bool isLongPacket = bytes.Length > 0x777F;
            headerWriter.WriteUInt16((ushort)(isLongPacket ? 0x8000 : bytes.Length + 9));
            if (isLongPacket)
            {
                headerWriter.WriteUInt32((uint)bytes.Length);
            }

            // Write body header
            var isControl = message.ServiceID == 0;
            headerWriter.WriteUInt8((byte)(isControl ? 1 : 0));                 // isControl
            headerWriter.WriteUInt8((byte)(isControl ? message.ServiceID : 0)); // opCode
            headerWriter.WriteUInt16(0);                                       // Padding
            var headerBytes = headerWriter.GetData();
            if (isControl)
            {
                // If this is a control message, we can simply craft the whole packet and return;
                var packet = new byte[bytes.Length + headerBytes.Length + 1];
                headerBytes.CopyTo(packet, 0);
                bytes.CopyTo(packet, headerBytes.Length);

                return packet;
            }
            else
            {
                // However, DML messages require a secondary header to be built.
                var dmlHeaderWriter = new BitBuffer();
                dmlHeaderWriter.WriteUInt8(message.ServiceID);
                dmlHeaderWriter.WriteUInt8((byte)(message.MessageOrder - 1));
                dmlHeaderWriter.WriteUInt16((ushort)(bytes.Length));

                var dmlHeaderBytes = dmlHeaderWriter.GetData();
                var packet = new byte[bytes.Length + dmlHeaderBytes.Length + headerBytes.Length + 1];
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
        /// <returns>An InetworkMessage object.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static INetworkMessage DeserializeMessageBinary(byte[] binaryBuffer)
        {
            if (binaryBuffer is null) throw new ArgumentNullException(nameof(binaryBuffer));
            if (binaryBuffer.Length <= 0) throw new ArgumentOutOfRangeException(nameof(binaryBuffer));

            // Create binary reader.
            var stream = new MemoryStream(binaryBuffer);
            var reader = new BitBuffer(stream);
            reader.ReadUInt16(); // Skip Magic header
            var length = reader.ReadUInt16();

            var isControl = reader.ReadBool();
            var opCode = reader.ReadUInt8();
            reader.ReadUInt16(); // Padding bytes.

            // If this is not a control message, it means it's a data message.
            // Data messages have a secondary header to indicate service ID and message ID.
            var svcid = (isControl ? (byte)0 : reader.ReadUInt8());
            var msgid = (isControl ? opCode : reader.ReadUInt8());
            if (!isControl) reader.ReadUInt16(); // Read DML length

            // Dispatch to the corresponding protocol and find message record.
            INetworkProtocol protocol = ProtocolDispatcher.Dispatch(svcid);
            INetworkMessage message = protocol?.Dispatch((byte)(msgid + 1));
            if (protocol is null || message is null) return null;

            // The binary buffer will be in order of the DML elements as they are written.
            // We can use Reflection to map these variables appropriately.
            var recordFields = message.GetType().GetFields()
                .Where(f => f.IsDefined(typeof(DMLElementAttribute), false));

            // First we'll check to see if the record we dispatched is valid.
            // Simply check how many fields there are in the record and compare them to the bytes we still have left.
            // The reason we don't use the DML payload length is to let this be ambigous between DML and control messages.
            var bytesLeft = reader.GetSize() - reader.GetCurrentStream().Position;
            if (!DoesRecordFitBounds(bytesLeft, message))
                throw new InvalidOperationException("Incorrect record! Record fields does not fit in the bounds of the binary buffer.");

            // Iterate through the record fields and read the appropriate type from the binary buffer.
            foreach (var field in recordFields)
            {
                var dmlType = GetDmlTypeFromAttr(field);
                var val = _dmlReaders[dmlType](reader);
                field.SetValue(message, val);
            }

            return message;
        }

        private static bool DoesRecordFitBounds(long byteCount, INetworkMessage message)
        {
            int currentCount = 0;
            var messageFields = Util.GetAttributesFromType<DMLElementAttribute>(message);
            foreach (var field in messageFields)
            {
                currentCount += GetDmlTypeSize(field.SerializedType);
            }

            return currentCount <= byteCount;
        } 

        private static DMLType GetDmlTypeFromAttr(FieldInfo field)
        {
            // Get the DMLType from the attribute.
            DMLElementAttribute[] dmlElements = field.GetCustomAttributes(typeof(DMLElementAttribute), false)
                .Cast<DMLElementAttribute>()
                .ToArray();
            if (dmlElements.Count() == 0)
            {
                Log.Logger.Error($"Attempted to get DMLField attribute from [{field.Name}], but it did not contain one. Returning 0 size.");
                return 0;
            }
            if (dmlElements.Count() > 1)
            {
                Log.Logger.Error($"DMLField [{field.Name}] contained duplicate attributes!");
            }

            return dmlElements[0].SerializedType;
        }

        private static byte GetDmlTypeSize(DMLType type)
        {
            if (!_dmlTypeSize.TryGetValue(type, out var size))
                throw new Exception($"Could not get size for DMLType [{type.ToString()}].");

            return size;
        }

    }
}
