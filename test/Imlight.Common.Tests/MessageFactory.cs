using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Logger;
using Imlight.Engine.DML;

namespace Imlight.Engine
{
    internal static class MessageFactory
    {

        /// <summary>
        /// Creates a DML record from binary.
        /// </summary>
        /// <param name="kiPacketBuffer">The byte array of the packet to decode.</param>
        /// <returns>A cloned DML record.</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        internal static DMLRecord DMLFromBinary(byte[] kiPacketBuffer)
        {
            // Create binary reader.
            Stream stream = new MemoryStream(kiPacketBuffer);
            BinaryReader reader = new BinaryReader(stream);

            // If something is in the workload pool, the KINP magic header and the length should not exist in the buffer.
            // If for whatever reason it still is, skip it here.
            if (kiPacketBuffer.AsSpan()[0..2] == stackalloc byte[2] { 0x0D, 0xF0 })
            {
                reader.BaseStream.Position = 2; // Skip KINP magic header.

                // Skipping the length requires a bit more, as the header will
                // change depending on the size of the packet.
                UInt16 tinySize = reader.ReadUInt16();
                if (tinySize >= 0x777F) reader.BaseStream.Position += 4; // This is a large packet. Skip the Uint32.
            }

            bool isControl = reader.ReadBoolean();
            if (isControl)
                throw new ArgumentException("This is a control message, and is not supported by this method!");

            reader.BaseStream.Position += 3; // Skip the opCode and padding bytes.

            // Now we can start working with the DML header.
            byte serviceId = reader.ReadByte();
            byte messageId = reader.ReadByte();
            UInt16 dmlSize = reader.ReadUInt16();

            // Now we have all the information we need.
            // Let's find the corresponding DML record and instantiate a proper C# object.
            if (!DMLDatabase.TryGetProtocolByID(serviceId, out var protocol))
                throw new Exception($"A DML protocol could not be found for service id {serviceId}!");

            // Create a trimmed byte array to set all the DML record fields appropriately.
            // To create the trimmed array, we're using the current position of the BinaryReader.
            // The BinaryReader has already read the DML header, so everything leftover are the raw DML elements.
            int len = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
            int originIndex = (int)(reader.BaseStream.Position);
            byte[] trimmedDmlArray = new byte[len];
            Array.Copy(kiPacketBuffer, originIndex, trimmedDmlArray, 0, len);
            DMLRecord record = protocol.CreateDMLRecordFromBinary(messageId, trimmedDmlArray);

            if (record == null)
                throw new Exception($"Could not create DML record!" +
                    $"\nProtocol = [{protocol.Name}]" +
                    $"\nMessageID = [{messageId}]");

            return record;
        }

    }
}
