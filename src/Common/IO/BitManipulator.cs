using System;
using System.IO;

namespace Imlight.Common.IO;

public abstract class BitManipulator : IDisposable {
    protected Stream Stream { get; set; }
    protected byte BitPosition = 8;
    protected byte BitValue;
    protected bool CompactLengths = false;

    // ctor
    public BitManipulator() {
        Stream = new MemoryStream();
    }

    // ctor
    public BitManipulator(Stream existingStream) {
        Stream = existingStream;
    }

    // ctor
    public BitManipulator(byte[] data) {
        Stream = new MemoryStream(data);
    }

    public BitManipulator WithCompactLengths() {
        CompactLengths = true;
        return this;
    }

    /// <summary>
    /// Returns where the current bit position of the read stream is.
    /// </summary>
    /// <returns>The bit position of the read stream.</returns>
    public int BitPos() {
        var offset = (int)Stream.Position - (BitPosition != 0 ? 1 : 0);
        return BitPosition + 8 * offset;
    }

    /// <summary>
    /// Gets the current stream as an binary array.
    /// </summary>
    /// <returns>Returns the current stream as binary an array.</returns>
    public byte[] GetData() {
        var data = new byte[Stream.Length];
        var pos = Stream.Position;

        Stream.Seek(0, SeekOrigin.Begin);
        for (int i = 0; i < data.Length; i++) {
            data[i] = (byte)Stream.ReadByte();
        }

        Stream.Seek(pos, SeekOrigin.Begin);
        return data;
    }

    /// <summary>
    /// Gets the rest of the current stream as a binary array.
    /// </summary>
    /// <returns>Returns the rest of the current stream as a binary array.</returns>
    public byte[] GetRelativeData() {
        var lastingDataLength = Stream.Length - Stream.Position;
        var data = new byte[lastingDataLength];
        var pos = Stream.Position;

        for (int i = 0; i < lastingDataLength; i++) {
            data[i] = (byte)Stream.ReadByte();
        }

        Stream.Seek(pos, SeekOrigin.Begin);
        return data;
    }

    /// <summary>
    /// Gets the length of the internal stream.
    /// </summary>
    /// <returns>Returns the length of the internal stream.</returns>
    public uint Count() {
        return (uint)Stream.Length;
    }

    /// <summary>
    /// Reset the current bit position.
    /// </summary>
    protected void ResetBitPos() {
        if (BitPosition > 7) {
            return;
        }

        BitPosition = 8;
        BitValue = 0;
    }

    /// <summary>
    /// Reverses the bits of a byte.
    /// </summary>
    /// <param name="b">The byte to be reversed.</param>
    /// <returns>The reversed byte.</returns>
    protected static byte Reverse(byte b) {
        var a = 0;
        for (int i = 0; i < 8; i++) {
            if ((b & (1 << i)) != 0) {
                a |= 1 << (7 - i);
            }
        }

        return (byte)a;
    }

    /// <summary>
    /// Seeks the bit to a certain position on the internal stream.
    /// </summary>
    /// <param name="bit">The bit position to seek to.</param>
    public void SeekBit(int bit) {
        var offset = bit / 8;
        var bitPos = bit % 8;

        Stream.Seek(offset, SeekOrigin.Begin);
        BitPosition = (byte)bitPos;
        BitValue = 0;
    }

    public void Dispose() {
        Stream.Dispose();
    }
}
