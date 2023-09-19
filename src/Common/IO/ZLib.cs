/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.IO;
using Ionic.Zlib;

namespace Imlight.Common.IO;

public class ZLib
{
    public static byte[] Inflate(Stream compressedStream, uint offset, uint length)
    {
        // Seek to the specified offset in the stream
        compressedStream.Seek(offset, SeekOrigin.Begin);

        var outputStream = new MemoryStream();

        using (var zlibStream = new ZlibStream(compressedStream, CompressionMode.Decompress))
        {
            var buffer = new byte[length];
            int bytesRead;

            while ((bytesRead = zlibStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
            }
        }

        outputStream.Seek(0, SeekOrigin.Begin);
        return outputStream.ToArray();
    }
        
    public static byte[] Deflate(Stream dataStream)
    {
        var outputStream = new MemoryStream();

        using (var zlibStream = new ZlibStream(outputStream, CompressionMode.Compress, CompressionLevel.Default, true))
        {
            dataStream.CopyTo(zlibStream);
        }

        outputStream.Seek(0, SeekOrigin.Begin);
        return outputStream.ToArray();
    }
}