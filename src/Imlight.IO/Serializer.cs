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
    public static class Serializer
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
        private static readonly IReadOnlyDictionary<DMLType, Func<KiNPBinaryReader, object>> _dmlReaders
            = new Dictionary<DMLType, Func<KiNPBinaryReader, object>>()
        {
            { DMLType.BYT, (r)   => r.ReadSByte()       },
            { DMLType.UBYT, (r)  => r.ReadByte()        },
            { DMLType.SHRT, (r)  => r.ReadInt16()       },
            { DMLType.USHRT, (r) => r.ReadUInt16()      },
            { DMLType.INT, (r)   => r.ReadInt32()       },
            { DMLType.UINT, (r)  => r.ReadUInt32()      },
            { DMLType.STR, (r)   => r.ReadSmallString() },
            { DMLType.WSTR, (r)  => r.ReadString()      },
            { DMLType.FLT, (r)   => r.ReadSingle()      },
            { DMLType.DBL, (r)   => r.ReadDouble()      },
            { DMLType.GID, (r)   => r.ReadUInt64()      },
        };
        private static readonly IReadOnlyDictionary<DMLType, Action<KiNPBinaryWriter, object>> _dmlWriters
            = new Dictionary<DMLType, Action<KiNPBinaryWriter, object>>()
        {
            { DMLType.BYT,   (r, v)   => r.WriteSBYT((sbyte)v)   },
            { DMLType.UBYT,  (r, v)   => r.WriteBYT((byte)v)     },
            { DMLType.SHRT,  (r, v)   => r.WriteSHRT((short)v)   },
            { DMLType.USHRT, (r, v)   => r.WriteUSHRT((ushort)v) },
            { DMLType.INT,   (r, v)   => r.WriteINT((int)v)      },
            { DMLType.UINT,  (r, v)   => r.WriteUINT((uint)v)    },
            { DMLType.STR,   (r, v)   => r.WriteSTR((string)v)   },
            { DMLType.WSTR,  (r, v)   => r.WriteWSTR((string)v)  },
            { DMLType.FLT,   (r, v)   => r.WriteFLT((float)v)    },
            { DMLType.DBL,   (r, v)   => r.WriteDBL((double)v)   },
            { DMLType.GID,   (r, v)   => r.WriteGID((ulong)v)    },
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
            var writer = new KiNPBinaryWriter();

            foreach (var element in elements)
            {
                // Get DMLElement attribute from this property.
                var dmlType = GetDmlTypeFromAttr(element);

                _dmlWriters[dmlType].Invoke(writer, element.GetValue(element));
            }

            // Now that all the elements have been defined, we can craft the header.
            // Refer to docs: https://w101r.github.io/rewritten-docs/documentation/KINP/packet-framing/

            var bytes = writer.GetBytes();

            // Write first packet header.
            using var headerWriter = new KiNPBinaryWriter();
            headerWriter.WriteMagicHeader();
            // Write large header or small header, depending on the size of the payload.
            // The +9 comes from the body header itself, and the trailing null byte.
            bool isLongPacket = bytes.Length > 0x777F;
            headerWriter.WriteUSHRT((ushort)(isLongPacket ? 0x8000 : bytes.Length + 9));
            if (isLongPacket)
            {
                headerWriter.WriteUINT((uint)bytes.Length);
            }

            // Write body header
            var isControl = message.ServiceID == 0;
            headerWriter.WriteBYT((byte)(isControl ? 1 : 0));                 // isControl
            headerWriter.WriteBYT((byte)(isControl ? message.ServiceID : 0)); // opCode
            headerWriter.WriteUSHRT(0);                                       // Padding
            var headerBytes = headerWriter.GetBytes();
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
                var dmlHeaderWriter = new KiNPBinaryWriter();
                dmlHeaderWriter.WriteBYT(message.ServiceID);
                dmlHeaderWriter.WriteBYT((byte)(message.MessageOrder - 1));
                dmlHeaderWriter.WriteUSHRT((ushort)(bytes.Length));

                var dmlHeaderBytes = dmlHeaderWriter.GetBytes();
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
            var reader = new KiNPBinaryReader(stream);
            reader.SkipHeader();

            var isControl = reader.ReadBoolean();
            var opCode = reader.ReadByte();
            reader.ReadUInt16(); // Padding bytes.

            // If this is not a control message, it means it's a data message.
            // Data messages have a secondary header to indicate service ID and message ID.
            var svcid = (isControl ? (byte)0 : reader.ReadByte());
            var msgid = (isControl ? opCode : reader.ReadByte());
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
            var bytesLeft = reader.BaseStream.Length - reader.BaseStream.Position;
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

        public static uint HashString(string input)
        {
            int result = 0;

            var shift1 = 0;
            var shift2 = 32;
            foreach (char c in input)
            {
                var cb = (byte)c;

                result ^= (cb - 32) << shift1;

                if (shift1 > 24)
                {
                    result ^= (cb - 32) >> shift2;
                    if (shift1 >= 27)
                    {
                        shift1 -= 32;
                        shift2 += 32;
                    }
                }
                shift1 += 5;
                shift2 -= 5;
            }

            if (result < 0)
                result = -result;

            return (uint)result;
        }
        
        public static uint HashPropertyName(string name, string type)
        {
            uint typeHash = HashString(type);
            var propHash = Djb2Hash(name) & 0x7FFF_FFFF;

            // MSB drop
            return (typeHash + propHash) & 0xFFFF_FFFF;
        }
        
        public static uint Djb2Hash(string str)
        {
            uint hash = 5381;

            for (int i = 0; i < str.Length; i++)
            {
                hash = ((hash << 5) + hash) + ((byte)str[(int)i]);
            }

            return hash;
        }

        private static bool DoesRecordFitBounds(long byteCount, INetworkMessage message)
        {
            int currentCount = 0;
            var messageFields = GetDmlElementsFromMessage(message);
            foreach (var field in messageFields)
            {
                currentCount += GetDmlTypeSize(field.SerializedType);
            }

            return currentCount <= byteCount;
        }

        private static IEnumerable<DMLElementAttribute> GetDmlElementsFromMessage(INetworkMessage message)
        {
            return message.GetType().GetFields()
                .Where(f => f.IsDefined(typeof(DMLElementAttribute), false))
                .Cast<DMLElementAttribute>();
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
