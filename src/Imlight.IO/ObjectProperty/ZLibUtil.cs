using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zlib;

namespace Imlight.IO.ObjectProperty
{
    internal static class ZLibUtil
    {
        public static byte[] Compress(byte[] bytes)
        {
            var compressed = ZlibStream.CompressBuffer(bytes);
            using var memStream = new MemoryStream(compressed.Length + 4);
            using var writer = new BinaryWriter(memStream);
            writer.Write(bytes.Length);
            writer.Write(compressed);
            return memStream.GetBuffer();
        }

        public static BitBuffer Decompress(BitBuffer buffer)
        {
            var decompressedSize = buffer.ReadUInt32();
            byte[] outBytes = new byte[decompressedSize];
            using (var stream = new ZlibStream(buffer.GetCurrentStream(), CompressionMode.Decompress))
            {
                int read = stream.Read(outBytes);
                if (read != decompressedSize) throw new EndOfStreamException($"Decompress: expected {decompressedSize} bytes, got {read}");
            }

            return new BitBuffer(outBytes);
        }

        public static BitBuffer Decompress(byte[] buffer)
        {
            var reader = new BitBuffer(buffer);
            var decompressedSize = reader.ReadUInt32();
            byte[] outBytes = new byte[decompressedSize];
            using (var stream = new ZlibStream(reader.GetCurrentStream(), CompressionMode.Decompress))
            {
                int read = stream.Read(outBytes);
                if (read != decompressedSize) throw new EndOfStreamException($"Decompress: expected {decompressedSize} bytes, got {read}");
            }

            return new BitBuffer(outBytes);
        }
    }
}
