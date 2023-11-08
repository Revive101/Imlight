using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System.IO;

namespace Imlight.Common.ObjectProperty;

public static class Compression {
    /// <summary>
    /// Compresses the given byte array using the Deflate algorithm with the best compression level.
    /// </summary>
    /// <param name="_bytes">The byte array to compress.</param>
    /// <returns>The compressed byte array.</returns>
    public static byte[] Compress(byte[] _bytes) {
        Deflater deflater = new(Deflater.BEST_COMPRESSION, false);
        deflater.SetInput(_bytes);
        deflater.Finish();

        using MemoryStream ms = new();
        byte[] outputBuffer = new byte[65536 * 4];
        while (deflater.IsNeedingInput == false) {
            ms.Write(outputBuffer, 0, deflater.Deflate(outputBuffer));

            if (deflater.IsFinished == true) {
                break;
            }
        }

        deflater.Reset();

        return ms.ToArray();
    }

    /// <summary>
    /// Decompresses a byte array using the InflaterInputStream class and returns the result as an IEnumerable of bytes.
    /// </summary>
    /// <param name="bytes">The byte array to decompress.</param>
    /// <returns>An IEnumerable of bytes representing the decompressed data.</returns>
    public static byte[] Decompress(byte[] bytes) {
        MemoryStream resStream = new();
        using (MemoryStream memoryStream = new(bytes)) {
            using InflaterInputStream inflater = new(memoryStream);
            inflater.CopyTo(resStream);
        }

        return resStream.ToArray();
    }
}
