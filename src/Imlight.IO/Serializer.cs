using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using Imlight.Internals;
using Imlight.Internals.DML;
using Imlight.Common.Logger;

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

        public static INetworkMessage DeserializeBinary(byte[] binaryBuffer)
        {
            if (binaryBuffer is null) throw new ArgumentNullException(nameof(binaryBuffer));
            if (binaryBuffer.Length <= 0) throw new ArgumentOutOfRangeException(nameof(binaryBuffer));

            // Create binary reader.
            MemoryStream stream = new MemoryStream(binaryBuffer);
            KiNPBinaryReader reader = new KiNPBinaryReader(stream);
            reader.SkipHeader();

            bool isControl = reader.ReadBoolean();
            byte opCode = reader.ReadByte();
            reader.ReadUInt16(); // Padding bytes.

            // If this is not a control message, it means it's a data message.
            // Data messages have a secondary header to indicate service ID and message ID.
            byte svcid = (isControl ? (byte)0 : reader.ReadByte());
            byte msgid = (isControl ? opCode : reader.ReadByte());
            if (!isControl) reader.ReadUInt16();

            // Dispatch to the corresponding protocol.
            INetworkProtocol protocol = ProtocolDispatcher.Dispatch(svcid);
            INetworkMessage message = protocol?.Dispatch(msgid);
            if (protocol is null || message is null) return null;

            // Log
            Log.Debug($"Deserialized message:\nProtocol: {nameof(protocol)}\nMessage: {nameof(message)}");

            // The binary buffer will be in order of the DML elements as they are written.
            // We can use Reflection to map these variables appropriately.
            var recordFields = message.GetType().GetFields()
                .Where(f => f.IsDefined(typeof(DMLElementAttribute), false));

            // First we'll check to see if the record we dispatched is valid.
            // Simply check how many fields there are in the record and compare them to the bytes we still have left.
            // The reason we don't use the DML payload length is to let this be ambigous between DML and control messages.
            long bytesLeft = reader.BaseStream.Length - reader.BaseStream.Position;
            if (!DoesRecordFitBounds(bytesLeft, recordFields))
                throw new InvalidOperationException("Incorrect record! Record fields does not fit in the bounds of the binary buffer.");

            // Iterate through the record fields and read the appropriate type from the binary buffer.
            foreach (var field in recordFields)
            {
                // Get the DMLType from the attribute.
                DMLElementAttribute dmlElement = field.GetCustomAttributes(typeof(DMLElementAttribute), false)
                    .Cast<DMLElementAttribute>()
                    .ToArray()[0];

                // This is a lazy solution.
                switch (dmlElement.SerializedType)
                {
                    case DMLType.BYT:
                        field.SetValue(message, reader.ReadSByte());
                        break;
                    case DMLType.UBYT:
                        field.SetValue(message, reader.ReadByte());
                        break;
                    case DMLType.SHRT:
                        field.SetValue(message, reader.ReadInt16());
                        break;
                    case DMLType.USHRT:
                        field.SetValue(message, reader.ReadUInt16());
                        break;
                    case DMLType.INT:
                        field.SetValue(message, reader.ReadInt32());
                        break;
                    case DMLType.UINT:
                        field.SetValue(message, reader.ReadUInt32());
                        break;
                    case DMLType.STR:
                    case DMLType.WSTR:
                        field.SetValue(message, reader.ReadString());
                        break;
                    case DMLType.FLT:
                        field.SetValue(message, reader.ReadSingle());
                        break;
                    case DMLType.DBL:
                        field.SetValue(message, reader.ReadDouble());
                        break;
                    case DMLType.GID:
                        field.SetValue(message, reader.ReadUInt64());
                        break;
                }
            }

            return message;
        }

        private static bool DoesRecordFitBounds(long byteCount, IEnumerable<FieldInfo> recordFields)
        {
            int currentCount = 0;
            foreach (FieldInfo field in recordFields)
            {
                // Get the DMLType from the attribute.
                DMLElementAttribute[] dmlElements = field.GetCustomAttributes(typeof(DMLElementAttribute), false)
                    .Cast<DMLElementAttribute>()
                    .ToArray();
                if (dmlElements.Length <= 0) continue; // This is a metadata field.

                // Grab the DML type from the first (and only) DMLElement attribute.
                // Find it's size value in the dictionary and add it to the total.
                DMLType dmlType = dmlElements[0].SerializedType;
                if (_dmlTypeSize.TryGetValue(dmlType, out var len))
                {
                    currentCount += len;
                }
                else
                {
                    throw new Exception($"Type of [{dmlType}] was not found in the size dictionary!");
                }
            }

            return currentCount <= byteCount;
        }

    }
}
