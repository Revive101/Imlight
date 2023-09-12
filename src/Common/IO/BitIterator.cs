/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using SharpDX;

namespace Imlight.Common.IO;

/// <summary>
/// A derived BinaryReader that supports bit manipulation. Supports both read and write.
/// </summary>
public class BitIterator : IDisposable
{
    private byte _bitPosition = 8;
    private byte _bitValue;
    private readonly BinaryWriter _writeStream;
    private readonly BinaryReader _readStream;
    public bool CompactLength;

    /// <summary>
    /// Creates a new <see cref="BitIterator"/> writer.
    /// </summary>
    /// <param name="compactLength">Determines if string lengths are serialized into 8-bits whenever possible.</param>
    public BitIterator(bool compactLength = false)
    {
        _writeStream = new BinaryWriter(new MemoryStream());
        CompactLength = compactLength;
    }

    /// <summary>
    /// Creates a new <see cref="BitIterator"/> from an u8 array.
    /// </summary>
    /// <param name="data">The u8 array. A new <see cref="MemoryStream"/> will be created from it.</param>
    /// <param name="compactLength">Determines if string lengths are serialized into 8-bits whenever possible.</param>
    public BitIterator(byte[] data, bool compactLength = false)
    {
        _readStream = new BinaryReader(new MemoryStream(data));
        CompactLength = compactLength;
    }

    /// <summary>
    /// Creates a new <see cref="BitIterator"/> from an existing stream.
    /// </summary>
    /// <param name="stream">The existing stream object. No seeking is done.</param>
    /// <param name="compactLength">Determines if string lengths are serialized into 8-bits whenever possible.</param>
    public BitIterator(Stream stream, bool compactLength = false)
    {
        _readStream = new BinaryReader(stream);
        CompactLength = compactLength;
    }

    #region Read Methods

    /// <summary>
    /// Reads a signed byte. Will reset the bit position.
    /// </summary>
    /// <returns>The signed byte that was read.</returns>
    public sbyte ReadInt8()
    {
        ResetBitPos();
        return _readStream.ReadSByte();
    }

    /// <summary>
    /// Reads an unsigned byte. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned byte that was read.</returns>
    public byte ReadUInt8()
    {
        ResetBitPos();
        return _readStream.ReadByte();
    }

    /// <summary>
    /// Reads a signed 16-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The signed 16-bit integer that was read.</returns>
    public short ReadInt16()
    {
        ResetBitPos();
        return _readStream.ReadInt16();
    }
        
    /// <summary>
    /// Reads an unsigned 16-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned 16-bit integer that was read.</returns>
    public ushort ReadUInt16()
    {
        ResetBitPos();
        return _readStream.ReadUInt16();
    }

    /// <summary>
    /// Reads a signed 32-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The signed 32-bit integer that was read.</returns>
    public int ReadInt32()
    {
        ResetBitPos();
        return _readStream.ReadInt32();
    }
        
    /// <summary>
    /// Reads an unsigned 32-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned 32-bit integer that was read.</returns>
    public uint ReadUInt32()
    {
        ResetBitPos();
        return _readStream.ReadUInt32();
    }

    /// <summary>
    /// Reads a signed 64-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned 64-bit integer that was read.</returns>
    public long ReadInt64()
    {
        ResetBitPos();
        return _readStream.ReadInt64();
    }

    /// <summary>
    /// Reads an unsigned 64-bit integer. Will reset the bit position.
    /// </summary>
    /// <returns>The unsigned 64-bit integer that was read.</returns>
    public ulong ReadUInt64()
    {
        ResetBitPos();
        return _readStream.ReadUInt64();
    }

    /// <summary>
    /// Reads a float value. Will reset the bit position.
    /// </summary>
    /// <returns>The float value that was read.</returns>
    public float ReadFloat()
    {
        ResetBitPos();
        return _readStream.ReadSingle();
    }

    /// <summary>
    /// Reads a 64-bit float value. Will reset the bit position.
    /// </summary>
    /// <returns>The 64-bit float value that was read.</returns>
    public double ReadDouble()
    {
        ResetBitPos();
        return _readStream.ReadDouble();
    }

    /// <summary>
    /// Reads a string. Will not reset the bit position.
    /// </summary>
    /// <returns>A <see cref="ByteString"/> representation of the string, which is interpreted as UTF-8</returns>
    public ByteString ReadString()
    {
        // If the compact string length bit is flagged, attempt to read a compressed length. If the length MSB is 1,
        // it is not compressed and still uses 16-bits.
        var length = CompactLength 
            ? ReadBits<int>(ReadBit() ? 15 : 7) 
            : _readStream.ReadUInt16();

        var bytes = ReadBytes(length);
        return new ByteString(bytes);
    }

    /// <summary>
    /// Reads a 32-bit prefixed string. Will not reset the bit position.
    /// </summary>
    /// <returns>A <see cref="ByteString"/> representation of the string, which is interpreted as UTF-8</returns>
    public ByteString ReadBigString()
    {
        ResetBitPos();
        var length = _readStream.ReadInt32();
        var bytes = ReadBytes(length);

        return new ByteString(bytes);
    }

    /// <summary>
    /// Reads an Unicode encoded string. Will not reset the bit position.
    /// </summary>
    /// <returns>The string that was read, interpreted as Unicode.</returns>
    public string ReadWString()
    {
        // If the compact string length bit is flagged, attempt to read a compressed length. If the length MSB is 1,
        // it is not compressed and still uses 16-bits.
        var length = CompactLength 
            ? ReadBits<int>(ReadBit() ? 15 : 7) 
            : _readStream.ReadUInt16();

        if (length == 0) 
            return string.Empty;
            
        // KingsIsle will still serialize the length as if it was a UTF-8 string, meaning we must double
        // the read length.
        var bytes = ReadBytes(length * 2);
        return Encoding.Unicode.GetString(bytes);
    }

    /// <summary>
    /// Reads a boolean value. Will reset the bit position.
    /// </summary>
    /// <returns>The boolean value that was read.</returns>
    public bool ReadBool()
    {
        ResetBitPos();
        return _readStream.ReadBoolean();
    }

    /// <summary>
    /// Read a certain amount of bytes from the stream. Will reset the bit position.
    /// </summary>
    /// <param name="count">The amount of bytes to read.</param>
    /// <returns>An u8 array of the bytes read.</returns>
    public byte[] ReadBytes(int count)
    {
        ResetBitPos();
        return _readStream.ReadBytes(count);
    }

    /// <summary>
    /// Reads a <see cref="Vector2"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Vector2"/> that was read.</returns>
    public Vector2 ReadVector2()
    {
        ResetBitPos();
        return new Vector2(
            _readStream.ReadSingle(),
            _readStream.ReadSingle());
    }

    /// <summary>
    /// Reads a <see cref="Vector3"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Vector3"/> that was read.</returns>
    public Vector3 ReadVector3()
    {
        ResetBitPos();
        return new Vector3(
            _readStream.ReadSingle(), 
            _readStream.ReadSingle(), 
            _readStream.ReadSingle());
    }

    /// <summary>
    /// Reads a <see cref="Quaternion"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Quaternion"/> that was read.</returns>
    public Quaternion ReadQuaternion()
    {
        ResetBitPos();
        return new Quaternion(
            _readStream.ReadSingle(), 
            _readStream.ReadSingle(), 
            _readStream.ReadSingle(), 
            _readStream.ReadSingle());
    }

    /// <summary>
    /// Reads a <see cref="Matrix3x3"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Matrix3x3"/> that was read.</returns>
    public Matrix3x3 ReadMatrix()
    {
        ResetBitPos();
        var m = new float[12];
        for (int i = 0; i < 12; i++)
        {
            m[i] = _readStream.ReadSingle();
        }

        return new Matrix3x3(m);
    }

    /// <summary>
    /// Reads a <see cref="Color"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Color"/> that was read.</returns>
    public Color ReadColor()
    {
        ResetBitPos();
        return new Color(
            _readStream.ReadByte(),
            _readStream.ReadByte(),
            _readStream.ReadByte());
    }

    /// <summary>
    /// Reads a <see cref="Color3"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Color3"/> that was read.</returns>
    public Color3 ReadColor3()
    {
        ResetBitPos();
        return new Color3(
            _readStream.ReadSingle(),
            _readStream.ReadSingle(),
            _readStream.ReadSingle());
    }

    /// <summary>
    /// Reads a <see cref="Rectangle"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="Rectangle"/> that was read.</returns>
    public Rectangle ReadRectangle()
    {
        ResetBitPos();
        return new Rectangle(
            _readStream.ReadInt32(),
            _readStream.ReadInt32(),
            _readStream.ReadInt32(),
            _readStream.ReadInt32());
    }

    /// <summary>
    /// Reads a <see cref="RectangleF"/>. Will reset the bit position.
    /// </summary>
    /// <returns>The <see cref="RectangleF"/> that was read.</returns>
    public RectangleF ReadRectangleF()
    {
        ResetBitPos();
        return new RectangleF(
            _readStream.ReadSingle(),
            _readStream.ReadSingle(),
            _readStream.ReadSingle(),
            _readStream.ReadSingle());
    }

    /// <summary>
    /// Reads a single bit from the stream. Will not reset the bit position, unless it is over 8.
    /// </summary>
    /// <returns>A boolean flag denoting the value of the bit that was read.</returns>
    public bool ReadBit()
    {
        if (_bitPosition == 8)
        {
            try
            {
                _bitValue = Reverse(ReadUInt8());
            }
            catch (EndOfStreamException)
            {
                _bitValue = 0;
            }
            _bitPosition = 0;
        }

        int returnValue = _bitValue;
        _bitValue <<= 1;
        _bitPosition++;

        return (returnValue & 0x80) != 0;
    }

    /// <summary>
    /// Reads a certain amount of bits. 
    /// </summary>
    /// <param name="bitCount">The amount of bits to read.</param>
    /// <typeparam name="T">The type that will be used to represent the bits read.</typeparam>
    /// <returns>The declared type T.</returns>
    public unsafe T ReadBits<T>(int bitCount) 
        where T : unmanaged
    {
        // Assert that it's not possible to create the given type T because it does not match the amount of bits
        // the user wants to read.
        Debug.Assert(sizeof(T) * 8 >= bitCount);

        var obj = new T();
        var ptr = (byte*)&obj;

        for (int i = 0; i < bitCount; i++)
        {
            if (i % 8 == 0 && i != 0)
            {
                ptr++;
            }

            if (ReadBit())
            {
                *ptr |= (byte)(1 << (i % 8));
            }
        }
            
        return obj;
    }

    #endregion

    #region Write Methods

    /// <summary>
    /// Writes a signed byte to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The signed byte to write to the stream.</param>
    public void WriteInt8(sbyte data)
    {
        FlushBits();
        _writeStream.Write(data);
    }
        
    /// <summary>
    /// Writes an unsigned byte to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The unsigned byte to write to the stream.</param>
    public void WriteUInt8(byte data)
    {
        FlushBits();
        _writeStream.Write(data);
    }

    /// <summary>
    /// Writes a signed 16-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 16-bit signed value to write to the stream.</param>
    public void WriteInt16(short data)
    {
        FlushBits();
        _writeStream.Write(data);
    }
        
    /// <summary>
    /// Writes an unsigned 16-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 16-bit unsigned value to write to the stream.</param>
    public void WriteUInt16(ushort data)
    {
        FlushBits();
        _writeStream.Write(data);
    }

    /// <summary>
    /// Writes a signed 32-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 32-bit signed value to write to the stream.</param>
    public void WriteInt32(int data)
    {
        FlushBits();
        _writeStream.Write(data);
    }
        
    /// <summary>
    /// Writes an unsigned 32-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 32-bit unsigned value to write to the stream.</param>
    public void WriteUInt32(uint data)
    {
        FlushBits();
        _writeStream.Write(data);
    }

    /// <summary>
    /// Writes a signed 64-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 64-bit signed value to write to the stream.</param>
    public void WriteInt64(long data)
    {
        FlushBits();
        _writeStream.Write(data);
    }

    /// <summary>
    /// Writes an unsigned 64-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 64-bit unsigned value to write to the stream.</param>
    public void WriteUInt64(ulong data)
    {
        FlushBits();
        _writeStream.Write(data);
    }

    /// <summary>
    /// Writes a 32-bit float value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 32-bit float value to write to the stream.</param>
    public void WriteFloat(float data)
    {
        FlushBits();
        _writeStream.Write(data);
    }

    /// <summary>
    /// Writes a 64-bit double value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 64-bit double value to write to the stream.</param>
    public void WriteDouble(double data)
    {
        FlushBits();
        _writeStream.Write(data);
    }

    /// <summary>
    /// Writes an UTF-8 encoded string to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="str">The UTF-8 encoded string to write to the stream.</param>
    public void WriteString(ByteString str)
    {
        if (str.ToString() is null)
        {
            if (CompactLength)
                WriteUInt8(0);
            else
                WriteUInt16(0);
            return;
        }

        if (CompactLength)
        {
            if (str.Length >= 128)
            {
                WriteBit(1);
                WriteBits(str.Length, 15);
            }
            else
            {
                WriteBit(0);
                WriteBits(str.Length, 7);
            }
        }
        else
        {
            WriteUInt16((ushort)str.Length);
            WriteBytes(str);
        }
    }

    /// <summary>
    /// Writes an UTF-16 encoded string to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="str">The UTF-16 encoded string to write to the stream.</param>
    public void WriteWString(string str)
    {
        if (CompactLength)
        {
            if (str.Length >= 128)
            {
                WriteBit(1);
                WriteBits(str.Length, 15);
            }
            else
            {
                WriteBit(0);
                WriteBits(str.Length, 7);
            }
        }
        else
        {
            WriteUInt16((ushort)str.Length);
            var bytes = Encoding.Unicode.GetBytes(str);
            WriteBytes(bytes);
        }
    }

    /// <summary>
    /// Writes a string to the packet with a null terminated (0)
    /// </summary>
    /// <param name="str"></param>
    public void WriteCString(ByteString str)
    {
        if (str.ToString() is null)
        {
            WriteUInt8(0);
            return;
        }

        WriteString(str);
        WriteUInt8(0);
    }
        
    /// <summary>
    /// Writes a binary array to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="data">The binary array to write to the stream.</param>
    public void WriteBytes(byte[] data)
    {
        FlushBits();
        _writeStream.Write(data, 0, data.Length);
    }

    /// <summary>
    /// Writes a <see cref="Color"/> to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="col">The <see cref="Color"/> to write to the stream.</param>
    public void WriteColor(Color col)
    {
        var color = new[] { col.R, col.G, col.B, col.A };
        _writeStream.Write(color);
    }

    /// <summary>
    /// Writes a <see cref="Color3"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="col">The <see cref="Color3"/> to write to the stream.</param>
    public void WriteColor3(Color3 col)
    {
        FlushBits();
        WriteFloat(col.Red);
        WriteFloat(col.Green);
        WriteFloat(col.Blue);
    }
        
    /// <summary>
    /// Writes a <see cref="Vector3"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="vec">The <see cref="Vector3"/> to write to the stream.</param>
    public void WriteVector3(Vector3 vec)
    {
        FlushBits();
        WriteFloat(vec.X);
        WriteFloat(vec.Y);
        WriteFloat(vec.Z);
    }

    /// <summary>
    /// Writes a <see cref="Quaternion"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="quad">The <see cref="Quaternion"/> to write to the stream.</param>
    public void WriteQuaternion(Quaternion quad)
    {
        FlushBits();
        WriteFloat(quad.X);
        WriteFloat(quad.Y);
        WriteFloat(quad.Z);
        WriteFloat(quad.W);
    }

    /// <summary>
    /// Writes a <see cref="Matrix"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="matrix">The <see cref="Matrix"/> to write to the stream.</param>
    public void WriteMatrix(Matrix matrix)
    {
        // Wizard101 only uses Matrix3x3.
        FlushBits();
        WriteFloat(matrix.M11);
        WriteFloat(matrix.M12);
        WriteFloat(matrix.M13);
        WriteFloat(matrix.M21);
        WriteFloat(matrix.M22);
        WriteFloat(matrix.M23);
        WriteFloat(matrix.M31);
        WriteFloat(matrix.M32);
        WriteFloat(matrix.M33);
        WriteFloat(matrix.M41);
        WriteFloat(matrix.M42);
        WriteFloat(matrix.M43);
    }

    /// <summary>
    /// Writes a <see cref="Rectangle"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="rec">The <see cref="Rectangle"/> to write to the stream.</param>
    public void WriteRectangle(Rectangle rec)
    {
        FlushBits();
        WriteInt32(rec.X);
        WriteInt32(rec.Y);
        WriteInt32(rec.Width);
        WriteInt32(rec.Height);
    }

    /// <summary>
    /// Writes a <see cref="RectangleF"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="rec">The <see cref="RectangleF"/> to write to the stream.</param>
    public void WriteRectangleF(RectangleF rec)
    {
        FlushBits();
        WriteFloat(rec.X);
        WriteFloat(rec.Y);
        WriteFloat(rec.Width);
        WriteFloat(rec.Height);
    }

    /// <summary>
    /// Writes a <see cref="Vector2"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="vec">The <see cref="Vector2"/> to write to the stream.</param>
    public void WriteVector2(Vector2 vec)
    {
        FlushBits();
        WriteFloat(vec.X);
        WriteFloat(vec.Y);
    }

    /// <summary>
    /// Writes a single bit to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="bit">The bit to write to the stream.</param>
    public void WriteBit(bool bit)
    {
        WriteBit(bit ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Writes a single bit to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="bit">The bit to write to the stream.</param>
    public void WriteBit(byte bit)
    {
        Debug.Assert(bit == 0 || bit == 1);

        --_bitPosition;

        if (bit == 1)
            _bitValue |= (byte)(1 << _bitPosition);
        if (_bitPosition == 0) FlushBits();
    }

    /// <summary>
    /// Writes a certain amount of bits to the stream, represented as a given type T.
    /// </summary>
    /// <param name="bit">The bit to be written to the stream, represented as a given type T.</param>
    /// <param name="count">The length of how many bits will be written to the stream.</param>
    /// <typeparam name="T">The given type.</typeparam>
    public unsafe void WriteBits<T>(T bit, int count) 
        where T : unmanaged
    {
        var ptr = (byte*)&bit;

        for (int i = 0; i < count; i++)
        {
            if (i % 8 == 0 && i != 0)
            {
                ptr++;
            }
            WriteBit((byte)((*ptr >> i % 8) & 1));
        }
    }

    #endregion
        
    /// <summary>
    /// Returns where the current bit position of the read stream is.
    /// </summary>
    /// <returns>The bit position of the read stream.</returns>
    public int TellBitPos()
    {
        if (_readStream == null) 
            return 8 - _bitPosition + 8 * (int)GetCurrentStream().Position;
        var offset = (int)GetCurrentStream().Position - (_bitPosition != 0 ? 1 : 0);
        return _bitPosition + 8 * offset;
    }

    /// <summary>
    /// Gets the current stream as an binary array.
    /// </summary>
    /// <returns>Returns the current stream as binary an array.</returns>
    public byte[] GetData()
    {
        var stream = GetCurrentStream();
        var data = new byte[stream.Length];
        var pos = stream.Position;
            
        stream.Seek(0, SeekOrigin.Begin);
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)stream.ReadByte();

        stream.Seek(pos, SeekOrigin.Begin);
        return data;
    }

    /// <summary>
    /// Gets the rest of the current stream as a binary array.
    /// </summary>
    /// <returns>Returns the rest of the current stream as a binary array.</returns>
    public byte[] GetRelativeData()
    {
        var stream = GetCurrentStream();
        var lastingDataLength = stream.Length - stream.Position;
        var data = new byte[lastingDataLength];
        var pos = stream.Position;
            
        for (int i = 0; i < lastingDataLength; i++)
            data[i] = (byte)stream.ReadByte();

        stream.Seek(pos, SeekOrigin.Begin);
        return data;
    }

    /// <summary>
    /// Gets the length of the internal stream.
    /// </summary>
    /// <returns>Returns the length of the internal stream.</returns>
    public uint Count()
    {
        return (uint)GetCurrentStream().Length;
    }

    /// <summary>
    /// Determines if this BitIterator is a read or write stream, and returns that stream.
    /// </summary>
    /// <returns>The internal stream.</returns>
    public Stream GetCurrentStream()
    {
        return _writeStream != null ? _writeStream.BaseStream : _readStream.BaseStream;
    }

    /// <summary>
    /// Seeks the bit to a certain position on the internal stream.
    /// </summary>
    /// <param name="bit">The bit position to seek to.</param>
    public void SeekBit(int bit)
    {
        GetCurrentStream().Position = bit >> 3;
        ResetBitPos();
        var remainingBits = bit - ((bit >> 3) << 3);
        Debug.Assert(remainingBits <= 8);
        ReadBits<byte>(remainingBits);
    }

    public void Dispose()
    {
        _writeStream?.Dispose();
        _readStream?.Dispose();
    }
        
    /// <summary>
    /// Flush the bits up until the next byte of the current write stream.
    /// </summary>
    /// <exception cref="Exception">Thrown if the internal stream is on read.</exception>
    /// <exception cref="NullReferenceException">Thrown if the internal write stream is null.</exception>
    private void FlushBits()
    {
        if (_readStream is not null)
            throw new Exception($"Cannot the flush bits of a read stream.");
        if (_writeStream is null)
            throw new NullReferenceException($"Cannot flush bits. {nameof(_writeStream)} is null.");
        if (_bitPosition == 8)
            return;

        _writeStream.Write(Reverse(_bitValue));
        _bitValue = 0;
        _bitPosition = 8;
    }

    /// <summary>
    /// Reset the current bit position.
    /// </summary>
    private void ResetBitPos()
    {
        Debug.Assert(_writeStream == null);

        if (_bitPosition > 7)
            return;

        _bitPosition = 8;
        _bitValue = 0;
    }
        
    /// <summary>
    /// Reverses the bits of a byte.
    /// </summary>
    /// <param name="b">The byte to be reversed.</param>
    /// <returns>The reversed byte.</returns>
    private static byte Reverse(byte b)
    {
        var a = 0;
        for (int i = 0; i < 8; i++)
            if ((b & (1 << i)) != 0)
                a |= 1 << (7 - i);
        return (byte)a;
    }
}