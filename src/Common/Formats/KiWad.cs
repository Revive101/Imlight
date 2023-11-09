using Imlight.Common.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace Imlight.Common.Formats;

/// <summary>
/// Represents the packed file header inside of a KIWAD.
/// </summary>
public sealed class FileRecord {
    public uint Offset { get; init; }
    public uint Size { get; init; }
    public uint CompressedSize { get; init; }
    public bool IsCompressed { get; init; }
    public uint Crc32 { get; init; }
    public string? FileName { get; init; }
}

/// <summary>
/// OOP wrapper for KiWad "Ki Where's All the Data" files.
/// </summary>
public class KiWad {
    public string? Name { get; set; }
    public uint Version { get; }
    public uint FileCount { get; }
    public uint Size { get; }
    public uint HeaderSize { get; }
    public Dictionary<string, FileRecord> Files { get; }

    private static readonly byte[] MagicHeader = "KIWAD"u8.ToArray();
    private readonly byte[] _data;

    // ctor
    public KiWad(Stream existingStream) {
        // Cache the KIWAD data into memory.
        // Todo: I really do not like this. I would like to be able to read from the stream directly.
        var ms = new MemoryStream();
        existingStream.CopyTo(ms);
        ms.Seek(0, SeekOrigin.Begin);
        this._data = ms.ToArray();

        var binaryReader = new BitReader(ms);

        // Validate that this is a KIWAD by reading the first 5 bytes, which should be "KIWAD".
        Span<byte> headerBuf = binaryReader.ReadBytes(MagicHeader.Length);
        if (!IsMagicHeader(headerBuf)) {
            throw new Exception("Stream does not contain a valid KIWAD header.");
        }

        this.Version = binaryReader.ReadUInt32();
        this.FileCount = binaryReader.ReadUInt32();
        this.Files = new Dictionary<string, FileRecord>();
        this.Size = (uint) ms.Length;

        // Newer versions have a padding byte here.
        if (Version >= 2) {
            _ = binaryReader.ReadBytes(1);
        }

        // Read through each file in this wad and create a FileRecord from the file header.
        for (int i = 0; i < FileCount; i++) {
            var fileRecord = ReadFileRecord(binaryReader);
            if (fileRecord.FileName != null) {
                Files.Add(fileRecord.FileName, fileRecord);
            }
        }

        HeaderSize = this.Size - (uint) binaryReader.GetRelativeData().Length;
    }

    /// <summary>
    /// Opens a stream to a file that may exist in this wad.
    /// </summary>
    /// <param name="fileName">The name of the file record.</param>
    /// <returns>The stream to the file, if it is found.</returns>
    /// <exception cref="NullReferenceException">If this KiWad is not properly initialized.</exception>
    public MemoryStream? OpenFile(string fileName) {
        // Check if this KiWad is properly initialized. If not, throw an exception.
        if (Files == null) {
            throw new NullReferenceException("Files dictionary must not be null. Is this KIWAD properly initialized?");
        }
        if (_data is null) {
            throw new NullReferenceException("Data is null. Has this wad been properly initialized?");
        }

        // Check if the file exists in this wad.
        if (!Files.TryGetValue($"{fileName}", out var fileRecord)) {
            return null;
        }

        // Create a new stream from the data.
        var ms = new MemoryStream(_data);

        // Seek to the offset of the FileRecord and inflate if necessary.
        ms.Seek(fileRecord.Offset, SeekOrigin.Begin);
        var buffer = fileRecord.IsCompressed
            ? ZLib.Inflate(ms, fileRecord.Offset, fileRecord.CompressedSize)
            : ReadFromStream(ms, fileRecord.Size);

        return new MemoryStream(buffer);
    }

    /// <summary>
    /// Gets the raw data of this KiWad.
    /// </summary>
    /// <returns>The raw data of this KiWad as a byte array.</returns>
    public byte[] GetData() => this._data;

    private static FileRecord ReadFileRecord(BitReader binaryReader) {
        var offset = binaryReader.ReadUInt32();
        var size = binaryReader.ReadUInt32();
        var compressedSize = binaryReader.ReadUInt32();
        var isCompressed = binaryReader.ReadBool();
        var crc32 = binaryReader.ReadUInt32();

        // Format the fileName to remove null terminate operator.
        var rawFileName = binaryReader.ReadBigString().ToString();
        if (rawFileName is "") {
            throw new Exception("Failed to read file name.");
        }

        var fileName = rawFileName.ToString().Replace("\0", "");

        return new FileRecord() {
            FileName = fileName,
            CompressedSize = compressedSize,
            Crc32 = crc32,
            IsCompressed = isCompressed,
            Offset = offset,
            Size = size,
        };
    }

    private static byte[] ReadFromStream(Stream stream, long size) {
        var buffer = new byte[size];
        var read = stream.Read(buffer, 0, (int) size);
        if (read != size) {
            throw new Exception($"{nameof(ReadFromStream)} did not read proper size. " +
                                $"GOT: {read} EXPECTED: {size}");
        }

        return buffer;
    }

    private static bool IsMagicHeader(Span<byte> header) {
        for (int i = 0; i < header.Length; i++) {
            if (header[i] != MagicHeader[i]) {
                return false;
            }
        }
        return true;
    }
}
