/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Imlight.Common.IO;

namespace Imlight.Common.Formats;

/// <summary>
/// Represents the packed file header inside of a KIWAD.
/// </summary>
public sealed class FileRecord
{
    public uint Offset { get; init; }
    public uint Size { get; init; }
    public uint CompressedSize { get; init; }
    public bool IsCompressed { get; init; }
    public uint Crc32 { get; init; }
    public string FileName { get; init; }
}
    
/// <summary>
/// Represents a KIWAD binary structure in memory. Contains a dictionary of <see cref="FileRecord"/> which can
/// be used to source individual files from the KIWAD.
/// <seealso cref="OpenFile"/>
/// </summary>
public class Wad
{
    public string Name { get; set; }
    public uint Version { get; }
    public uint FileCount { get; }
    public Dictionary<string, FileRecord> Files { get; }
        
    // LatestFile properties.
    public uint Size { get; }
    public uint HeaderSize { get; }

    private static readonly byte[] MagicHeader = "KIWAD"u8.ToArray();
    private readonly byte[] _data;

    /// <summary>
    /// Creates a new KIWAD from an existing stream. The stream will seek to the start of the given stream.
    /// </summary>
    /// <param name="existingStream"></param>
    /// <exception cref="WadNotValidException"></exception>
    public Wad(Stream existingStream)
    {
        // Cache the KIWAD data into memory.
        var ms = new MemoryStream();
        existingStream.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        this._data = ms.ToArray();
            
        var binaryReader = new BitIterator(ms);

        // Validate that this is a KIWAD by reading the first 5 bytes, which should be "KIWAD".
        Span<byte> headerBuf = binaryReader.ReadBytes(5);
        if (!IsMagicHeader(headerBuf))
            throw new Exception("Stream does not contain a valid KIWAD header.");

        this.Version = binaryReader.ReadUInt32();
        this.FileCount = binaryReader.ReadUInt32();
        this.Files = new Dictionary<string, FileRecord>();
        this.Size = (uint)ms.Length;

        // Newer versions have a padding byte here.
        if (Version >= 2)
            binaryReader.ReadBytes(1);

        // Read through each file in this wad and create a FileRecord from the file header.
        for (int i = 0; i < FileCount; i++)
        {
            var offset = binaryReader.ReadUInt32();
            var size = binaryReader.ReadUInt32();
            var compressedSize = binaryReader.ReadUInt32();
            var isCompressed = binaryReader.ReadBool();
            var crc32 = binaryReader.ReadUInt32();

            // Format the fileName to remove null terminate operator.
            var rawFileName = binaryReader.ReadBigString();
            var fileName = rawFileName.ToString().Replace("\0", "");

            var fileRecord = new FileRecord()
            {
                FileName = fileName,
                CompressedSize = compressedSize,
                Crc32 = crc32,
                IsCompressed = isCompressed,
                Offset = offset,
                Size = size,
            };
            Files.Add(fileName, fileRecord);
        }

        HeaderSize = this.Size - (uint)binaryReader.GetRelativeData().Length;
    }

    /// <summary>
    /// Opens a stream to a file that may exist in this wad.
    /// </summary>
    /// <param name="fileName">The name of the file record.</param>
    /// <returns>The stream to the file, if it is found.</returns>
    /// <exception cref="NullReferenceException">If this KIWAD is not properly initialized.</exception>
    /// <exception cref="WadFileNotValidException">If the file could not be found by the given name.</exception>
    public MemoryStream OpenFile(string fileName)
    {
        if (Files == null)
            throw new NullReferenceException("Files dictionary must not be null. Is this KIWAD properly initialized?");
        if (!Files.TryGetValue($"{fileName}", out var fileRecord)) 
            throw new Exception($"Could not find file by name [{fileName}] in KIWAD [{Name}]");
        if (_data is null)
            throw new NullReferenceException("Data is null. Has this wad been properly initialized?");

        // Create a new stream from the data.
        var ms = new MemoryStream(_data);
            
        // Seek to the offset of the FileRecord and inflate if necessary.
        ms.Seek(fileRecord.Offset, SeekOrigin.Begin);
        var buffer = fileRecord.IsCompressed
            ? ZLib.Inflate(ms, fileRecord.Offset, fileRecord.CompressedSize)
            : ReadFromStream(ms, fileRecord.Size);

        return new MemoryStream(buffer);
    }

    public byte[] GetData() => this._data;
        
    private static byte[] ReadFromStream(Stream stream, long size)
    {
        var buffer = new byte[size];
        var read = stream.Read(buffer, 0, (int)size);
        if (read != size)
            throw new Exception($"{nameof(ReadFromStream)} did not read proper size. " +
                                $"GOT: {read} EXPECTED: {size}");
        return buffer;
    }

    private static bool IsMagicHeader(Span<byte> header)
    {
        for (int i = 0; i < header.Length; i++)
        {
            if (header[i] != MagicHeader[i])
            {
                return false;
            }
        }
        return true;
    }
}