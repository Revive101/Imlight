using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Logger;

namespace Imlight.Engine
{
    internal static class MessageFactory
    {

        /// <summary>
        /// Creates a KIPacket object from a NetworkStream. Throws an error upon failure.
        /// </summary>
        /// <param name="stream">The NetworkStream referenced.</param>
        /// <returns>A KIPacket object.</returns>
        /// <exception cref="ArgumentException">The NetworkStream given does not contain KI's start signal.</exception>
        internal static KIPacket CreateKIPacketFromStream(NetworkStream stream)
        {
            // Validate if the stream contains KI's start signal.
            if (!IsKIPacket(stream)) throw new ArgumentException("Stream does not contain KI's start signal! " +
                "Ensure that the Network Stream is a valid KI packet before calling this method.");

            // Create binary reader. This single reader will be used for the entire stream.
            BinaryReader reader = new BinaryReader(stream);

            // Create empty packet object.
            KIPacket packet = new KIPacket();

            // Craft header
            KIPacket.Header header = new KIPacket.Header();

            try
            {
                // Skip start signal.
                reader.BaseStream.Position = 4;

                // The header's size will change depending on the length of the message.
                // If the length of the packet is greater than a uInt16, it uses a uInt32 instead.
                header.length = reader.ReadUInt16();
                if (header.length > 0x7FFF)
                {
                    // This is a big header
                    // Reverse base stream by uInt16 length, then read proper length
                    reader.BaseStream.Position -= 4;
                    header.bigLength = reader.ReadUInt32();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }

            // Finally, attach crafted header to empty packet object.
            packet.PacketHeader = header;

            return packet;

        }

        /// <summary>
        /// Validates whether a packet stream contains KI's start signal.
        /// </summary>
        /// <param name="stream">The NetworkStream object from the socket data.</param>
        /// <returns>True, if the start signal is attached to this stream. False otherwise.</returns>
        /// <exception cref="ArgumentNullException">The NetworkStream object must be valid.</exception>
        internal static bool IsKIPacket(NetworkStream stream)
        {
            //@todo: This function should be expanded to check the integrity of a packet.
            // Making sure length is valid, start signal, etc.
            if (stream is null) throw new ArgumentNullException(nameof(stream));

            // KINP prefixes smessages with a 2-byte sequence, "0xF00D", in little-endian byte order.
            // This is called the "Start Signal".
            if (stream.CanRead)
            {
                BinaryReader br = new BinaryReader(stream);
                try
                {
                    var header = br.ReadUInt16();
                    if (header == 0xF00D) return true;

                    else return false;
                }
                catch (EndOfStreamException ex)
                {
                    Log.Error(ex.Message);
                    return false;
                }
            }
            else
            {
                // This shouldn't be possible!
                // @todo: Not sure if RemoteEndPoint has a good implicit string cast. Should check.
                Log.Error($"Network stream from [{stream.Socket.RemoteEndPoint}] is not capable of being read!");
                return false;
            }
        }

    }
}
