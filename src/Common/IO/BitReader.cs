using System;
using System.IO;
using System.Text;
using SharpDX;

namespace Imlight.Common.IO;

/// <summary>
/// Wrapper around a BinaryReader that allows reading individual bits.
/// </summary>
public class BitReader : BitManipulator {
    private readonly BinaryReader _reader;

    /// <summary>
    /// Initializes a new instance of the BitReader class with an existing stream.
    /// </summary>
    /// <param name="existingStream">The existing stream to use.</param>
    public BitReader(Stream existingStream) {
        base.Stream = existingStream;
        _reader = new BinaryReader(Stream);
    }

    /// <summary>
    /// Initializes a new instance of the BitReader class with the specified byte array.
    /// </summary>
    /// <param name="data">The byte array to use.</param>
    public BitReader(byte[] data) {
        base.Stream = new MemoryStream(data);
        _reader = new BinaryReader(Stream);
    }

    /// <summary>
    /// Reads a signed byte. Will reset the bit position.
    /// </summary>
    /// <returns>The signed byte that was read.</returns>
    public sbyte ReadInt8() {
        ResetBitPos();
        return _reader.ReadSByte();
    }

    /// <summary>
    /// Reads an unsigned byte. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned byte that was read.</returns>
    public byte ReadUInt8() {
        ResetBitPos();
        return _reader.ReadByte();
    }

    /// <summary>
    /// Reads a signed 16-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The signed 16-bit integer that was read.</returns>
    public short ReadInt16() {
        ResetBitPos();
        return _reader.ReadInt16();
    }

    /// <summary>
    /// Reads an unsigned 16-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned 16-bit integer that was read.</returns>
    public ushort ReadUInt16() {
        ResetBitPos();
        return _reader.ReadUInt16();
    }

    /// <summary>
    /// Reads a signed 32-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The signed 32-bit integer that was read.</returns>
    public int ReadInt32() {
        ResetBitPos();
        return _reader.ReadInt32();
    }

    /// <summary>
    /// Reads an unsigned 32-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned 32-bit integer that was read.</returns>
    public uint ReadUInt32() {
        ResetBitPos();
        return _reader.ReadUInt32();
    }

    /// <summary>
    /// Reads a signed 64-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned 64-bit integer that was read.</returns>
    public long ReadInt64() {
        ResetBitPos();
        return _reader.ReadInt64();
    }

    /// <summary>
    /// Reads an unsigned 64-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned 64-bit integer that was read.</returns>
    public ulong ReadUInt64() {
        ResetBitPos();
        return _reader.ReadUInt64();
    }

    /// <summary>
    /// Reads a float value. Will reset the bit position.
    /// </summary>
    /// <returns>The float value that was read.</returns>
    public float ReadFloat() {
        ResetBitPos();
        return _reader.ReadSingle();
    }

    /// <summary>
    /// Reads a 64-bit float value. Will reset the bit position.
    /// </summary>
    /// <returns>The 64-bit float value that was read.</returns>
    public double ReadDouble() {
        ResetBitPos();
        return _reader.ReadDouble();
    }

    /// <summary>
    /// Reads a string. Will not reset the bit position.
    /// </summary>
    /// <returns>A <see cref="ByteString"/> representation of the string, which is interpreted as UTF-8</returns>
    public ByteString ReadString() {
        // If the compact string length bit is flagged, attempt to read a compressed length. If the length MSB is 1,
        // it is not compressed and still uses 16-bits.
        var length = base.CompactLengths
            ? ReadBits<int>(ReadBit() ? 15 : 7)
            : _reader.ReadUInt16();

        var bytes = ReadBytes(length);
        return new ByteString(bytes);
    }

    /// <summary>
    /// Reads a 32-bit prefixed string. Will not reset the bit position.
    /// </summary>
    /// <returns>A <see cref="ByteString"/> representation of the string, which is interpreted as UTF-8</returns>
    public ByteString ReadBigString() {
        ResetBitPos();
        var length = _reader.ReadInt32();
        var bytes = ReadBytes(length);

        return new ByteString(bytes);
    }

    /// <summary>
    /// Reads an Unicode encoded string. Will not reset the bit position.
    /// </summary>
    /// <returns>The string that was read, interpreted as Unicode.</returns>
    public string ReadWString() {
        // If the compact string length bit is flagged, attempt to read a compressed length. If the length MSB is 1,
        // it is not compressed and still uses 16-bits.
        var length = base.CompactLengths
            ? ReadBits<int>(ReadBit() ? 15 : 7)
            : _reader.ReadUInt16();

        if (length == 0) {
            return string.Empty;
        }

        // KingsIsle will still serialize the length as if it was a UTF-8 string, meaning we must double
        // the read length.
        var bytes = ReadBytes(length * 2);
        return Encoding.Unicode.GetString(bytes);
    }

    /// <summary>
    /// Reads a boolean value. Will reset the bit position.
    /// </summary>
    /// <returns>The boolean value that was read.</returns>
    public bool ReadBool() {
        ResetBitPos();
        return _reader.ReadBoolean();
    }

    /// <summary>
    /// Read a certain amount of bytes from the stream. Will reset the bit position.
    /// </summary>
    /// <param name="count">The amount of bytes to read.</param>
    /// <returns>An u8 array of the bytes read.</returns>
    public byte[] ReadBytes(int count) {
        ResetBitPos();
        return _reader.ReadBytes(count);
    }

    /// <summary>
    /// Reads a <see cref="Vector2"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Vector2"/> that was read.</returns>
    public Vector2 ReadVector2() {
        ResetBitPos();
        return new Vector2(
            _reader.ReadSingle(),
            _reader.ReadSingle());
    }

    /// <summary>
    /// Reads a <see cref="Vector3"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Vector3"/> that was read.</returns>
    public Vector3 ReadVector3() {
        ResetBitPos();
        return new Vector3(
            _reader.ReadSingle(),
            _reader.ReadSingle(),
            _reader.ReadSingle());
    }

    /// <summary>
    /// Reads a <see cref="Quaternion"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Quaternion"/> that was read.</returns>
    public Quaternion ReadQuaternion() {
        ResetBitPos();
        return new Quaternion(
            _reader.ReadSingle(),
            _reader.ReadSingle(),
            _reader.ReadSingle(),
            _reader.ReadSingle());
    }

    /// <summary>
    /// Reads a <see cref="Matrix3x3"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Matrix3x3"/> that was read.</returns>
    public Matrix3x3 ReadMatrix() {
        ResetBitPos();
        var m = new float[12];
        for (int i = 0; i < 12; i++) {
            m[i] = _reader.ReadSingle();
        }

        return new Matrix3x3(m);
    }

    /// <summary>
    /// Reads a <see cref="Color"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Color"/> that was read.</returns>
    public Color ReadColor() {
        ResetBitPos();
        return new Color(
            _reader.ReadByte(),
            _reader.ReadByte(),
            _reader.ReadByte());
    }

    /// <summary>
    /// Reads a <see cref="Color3"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Color3"/> that was read.</returns>
    public Color3 ReadColor3() {
        ResetBitPos();
        return new Color3(
            _reader.ReadSingle(),
            _reader.ReadSingle(),
            _reader.ReadSingle());
    }

    /// <summary>
    /// Reads a <see cref="Rectangle"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Rectangle"/> that was read.</returns>
    public Rectangle ReadRectangle() {
        ResetBitPos();
        return new Rectangle(
            _reader.ReadInt32(),
            _reader.ReadInt32(),
            _reader.ReadInt32(),
            _reader.ReadInt32());
    }

    /// <summary>
    /// Reads a <see cref="RectangleF"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="RectangleF"/> that was read.</returns>
    public RectangleF ReadRectangleF() {
        ResetBitPos();
        return new RectangleF(
            _reader.ReadSingle(),
            _reader.ReadSingle(),
            _reader.ReadSingle(),
            _reader.ReadSingle());
    }

    /// <summary>
    /// Reads a single bit from the stream. Will not reset the bit position, unless it is over 8.
    /// </summary>
    /// <returns>A boolean flag denoting the value of the bit that was read.</returns>
    public bool ReadBit() {
        if (base.BitPosition == 8) {
            try {
                base.BitValue = Reverse(ReadUInt8());
            }
            catch (EndOfStreamException) {
                base.BitValue = 0;
            }
            base.BitPosition = 0;
        }

        int returnValue = base.BitValue;
        base.BitValue <<= 1;
        base.BitPosition++;

        return (returnValue & 0x80) != 0;
    }

    /// <summary>
    /// Reads a certain amount of bits.
    /// </summary>
    /// <param name="bitCount">The amount of bits to read.</param>
    /// <typeparam name="T">The type that will be used to represent the bits read.</typeparam>
    /// <returns>The declared type T.</returns>
    public unsafe T ReadBits<T>(int bitCount) where T : unmanaged {
        //if (sizeof(T) * 8 >= bitCount) {
        //    throw new ArgumentException("The type T is too large to hold the amount of bits specified.");
        //}

        var obj = new T();
        var ptr = (byte*) &obj;

        for (int i = 0; i < bitCount; i++) {
            if (i % 8 == 0 && i != 0) {
                ptr++;
            }

            if (ReadBit()) {
                *ptr |= (byte) (1 << (i % 8));
            }
        }

        return obj;
    }
}
