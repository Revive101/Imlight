using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Imlight.IO
{
    /// <summary>
    /// Inherits BinaryReader, and provides extra functionality to read Kingsisle binary structures.
    /// </summary>
    public sealed partial class KiNPBinaryReader : BinaryReader, IDisposable, ICloneable
    {

        // ctor
        public KiNPBinaryReader(Stream input) : base(input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new Exception("Stream is not readable!");
            if (!IsKiNPPacket(input)) throw new Exception($"{nameof(KiNPBinaryReader)} does not support anything other than a valid KI packet!");
        }

        internal string ReadSmallString()
        {
            int strLen = base.ReadInt32();
            return Encoding.UTF8.GetString(base.ReadBytes(strLen));
        }

        /// <summary>
        /// Attempts to skip a KiNP packet header, if one exists.
        /// </summary>
        /// <returns>True, if this buffer is a valid KiNP packet. False otherwise.</returns>
        public bool SkipHeader()
        {
            if (this.BaseStream is null) return false;
            if (!IsKiNPPacket(this.BaseStream)) return false;

            // Skip magic header
            base.BaseStream.Position += 2;

            // The next part is the length, which changes sizes depending on the size of this packet.
            // If the packet is over the size of a uint_16, the following 4 bytes will be a uint_32, which is the actual length.
            UInt16 size = base.ReadUInt16();
            if (size > 0x777F)
            {
                // This is a large packet. Skip another 4 bytes for the replacement uint_32.
                base.BaseStream.Position += 4;
            }

            return true;
        }

        internal byte[] GetBytes()
        {
            using var memStream = new MemoryStream();
            base.BaseStream.CopyTo(memStream);
            return memStream.ToArray();
        }

        public object Clone()
        {
            throw new NotImplementedException();
        }

        public new void Dispose()
        {
            base.Close();
            base.Dispose();

            GC.SuppressFinalize(this);
        }

        #region Static Methods

        public static bool IsKiNPPacket(byte[] rawPacket)
            => (rawPacket.AsSpan()[0..2].SequenceEqual(stackalloc byte[2] { 0x0D, 0xF0 }));

        public static bool IsKiNPPacket(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            var header = reader.ReadUInt16();

            return header == 0xF00D;
        }

        #endregion

    }
}
