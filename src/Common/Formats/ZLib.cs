using System.IO;
using Ionic.Zlib;

namespace Imlight.Common.Formats;

public class ZLib {
    /// <summary>
    /// Decompresses a stream of compressed data using the Zlib Inflate algorithm.
    /// </summary>
    /// <param name="compressedStream">The compressed data stream to decompress.</param>
    /// <param name="offset">The offset in the stream where the compressed data starts.</param>
    /// <param name="length">The length of the compressed data in the stream.</param>
    /// <returns>The decompressed data as a byte array.</returns>
    public static byte[] Inflate(Stream compressedStream, uint offset, uint length) {
        // Seek to the specified offset in the stream
        compressedStream.Seek(offset, SeekOrigin.Begin);

        var outputStream = new MemoryStream();

        using (var zlibStream = new ZlibStream(compressedStream, CompressionMode.Decompress)) {
            var buffer = new byte[length];
            int bytesRead;

            while ((bytesRead = zlibStream.Read(buffer, 0, buffer.Length)) > 0) {
                outputStream.Write(buffer, 0, bytesRead);
            }
        }

        outputStream.Seek(0, SeekOrigin.Begin);
        return outputStream.ToArray();
    }

    /// <summary>
    /// Compresses the data in the given stream using the Zlib Deflate algorithm.
    /// </summary>
    /// <param name="dataStream">The stream containing the data to be compressed.</param>
    /// <returns>A byte array containing the compressed data.</returns>
    public static byte[] Deflate(Stream dataStream) {
        var outputStream = new MemoryStream();

        using (var zlibStream = new ZlibStream(outputStream, CompressionMode.Compress, CompressionLevel.Default, true)) {
            dataStream.CopyTo(zlibStream);
        }

        outputStream.Seek(0, SeekOrigin.Begin);
        return outputStream.ToArray();
    }
}
