/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using System.Text;
using SharpDX;

namespace Imlight.Common.IO;

public class BitWriter : BitManipulator {
    private readonly BinaryWriter _writer;

    /// <summary>
    /// Initializes a new instance of the BitWriter class with an empty memory stream.
    /// </summary>
    public BitWriter() {
        base.Stream = new MemoryStream();
        _writer = new BinaryWriter(Stream);
    }

    /// <summary>
    /// Initializes a new instance of the BitWriter class with an existing stream.
    /// </summary>
    /// <param name="existingStream">The existing stream to use.</param>
    public BitWriter(Stream existingStream) {
        base.Stream = existingStream;
        _writer = new BinaryWriter(Stream);
    }

    /// <summary>
    /// Initializes a new instance of the BitWriter class with the specified byte array.
    /// </summary>
    /// <param name="data">The byte array to use.</param>
    public BitWriter(byte[] data) {
        base.Stream = new MemoryStream(data);
        _writer = new BinaryWriter(Stream);
    }

    /// <summary>
    /// Writes a signed byte to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The signed byte to write to the stream.</param>
    public void WriteInt8(sbyte data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes an unsigned byte to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The unsigned byte to write to the stream.</param>
    public void WriteUInt8(byte data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes a signed 16-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 16-bit signed value to write to the stream.</param>
    public void WriteInt16(short data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes an unsigned 16-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 16-bit unsigned value to write to the stream.</param>
    public void WriteUInt16(ushort data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes a signed 32-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 32-bit signed value to write to the stream.</param>
    public void WriteInt32(int data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes an unsigned 32-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 32-bit unsigned value to write to the stream.</param>
    public void WriteUInt32(uint data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes a signed 64-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 64-bit signed value to write to the stream.</param>
    public void WriteInt64(long data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes an unsigned 64-bit value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 64-bit unsigned value to write to the stream.</param>
    public void WriteUInt64(ulong data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes a 32-bit float value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 32-bit float value to write to the stream.</param>
    public void WriteFloat(float data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes a 64-bit double value to the stream. Will flush the bits prior.
    /// </summary>
    /// <param name="data">The 64-bit double value to write to the stream.</param>
    public void WriteDouble(double data) {
        FlushBits();
        _writer.Write(data);
    }

    /// <summary>
    /// Writes an UTF-8 encoded string to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="str">The UTF-8 encoded string to write to the stream.</param>
    public void WriteString(ByteString str) {
        if (str.ToString() is null || str.ToString() == string.Empty) {
            if (base.CompactLengths) {
                WriteUInt8(0);
            }
            else {
                WriteUInt16(0);
            }

            return;
        }

        if (base.CompactLengths) {
            if (str.Length >= 128) {
                WriteBit(1);
                WriteBits(str.Length, 15);
            }
            else {
                WriteBit(0);
                WriteBits(str.Length, 7);
            }
        }
        else {
            WriteUInt16((ushort)str.Length);
            WriteBytes(str);
        }
    }

    /// <summary>
    /// Writes an UTF-16 encoded string to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="str">The UTF-16 encoded string to write to the stream.</param>
    public void WriteWString(string str) {
        if (base.CompactLengths) {
            if (str.Length >= 128) {
                WriteBit(1);
                WriteBits(str.Length, 15);
            }
            else {
                WriteBit(0);
                WriteBits(str.Length, 7);
            }
        }
        else {
            WriteUInt16((ushort)str.Length);
            var bytes = Encoding.Unicode.GetBytes(str);
            WriteBytes(bytes);
        }
    }

    /// <summary>
    /// Writes a string to the packet with a null terminated (0)
    /// </summary>
    /// <param name="str"></param>
    public void WriteCString(ByteString str) {
        if (str.ToString() is null || str.ToString() == string.Empty) {
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
    public void WriteBytes(byte[] data) {
        FlushBits();
        _writer.Write(data, 0, data.Length);
    }

    /// <summary>
    /// Writes a <see cref="Color"/> to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="col">The <see cref="Color"/> to write to the stream.</param>
    public void WriteColor(Color col) {
        var color = new[] { col.R, col.G, col.B, col.A };
        _writer.Write(color);
    }

    /// <summary>
    /// Writes a <see cref="Vector3"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="vec">The <see cref="Vector3"/> to write to the stream.</param>
    public void WriteVector3(Vector3 vec) {
        FlushBits();
        WriteFloat(vec.X);
        WriteFloat(vec.Y);
        WriteFloat(vec.Z);
    }

    /// <summary>
    /// Writes a <see cref="Quaternion"/> to the stream. The bits will be flushed prior.
    /// </summary>
    /// <param name="quad">The <see cref="Quaternion"/> to write to the stream.</param>
    public void WriteQuaternion(Quaternion quad) {
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
    public void WriteMatrix(Matrix matrix) {
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
    public void WriteRectangle(Rectangle rec) {
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
    public void WriteRectangleF(RectangleF rec) {
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
    public void WriteVector2(Vector2 vec) {
        FlushBits();
        WriteFloat(vec.X);
        WriteFloat(vec.Y);
    }

    /// <summary>
    /// Writes a single bit to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="bit">The bit to write to the stream.</param>
    public void WriteBit(bool bit) {
        WriteBit(bit ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Writes a single bit to the stream. The bits will not be flushed prior.
    /// </summary>
    /// <param name="bit">The bit to write to the stream.</param>
    public void WriteBit(byte bit) {
        --base.BitPosition;

        if (bit == 1) {
            base.BitValue |= (byte)(1 << base.BitPosition);
        }
        if (base.BitPosition == 0) {
            FlushBits();
        }
    }

    /// <summary>
    /// Writes a certain amount of bits to the stream, represented as a given type T.
    /// </summary>
    /// <param name="bit">The bit to be written to the stream, represented as a given type T.</param>
    /// <param name="count">The length of how many bits will be written to the stream.</param>
    /// <typeparam name="T">The given type.</typeparam>
    public unsafe void WriteBits<T>(T bit, int count) where T : unmanaged {
        var ptr = (byte*)&bit;

        for (int i = 0; i < count; i++) {
            if (i % 8 == 0 && i != 0) {
                ptr++;
            }
            WriteBit((byte)((*ptr >> i % 8) & 1));
        }
    }

    private void FlushBits() {
        if (_writer is null) {
            throw new NullReferenceException($"Cannot flush bits. {nameof(_writer)} is null.");
        }
        else if (base.BitPosition == 8) {
            return;
        }

        _writer.Write(Reverse(base.BitValue));
        base.BitValue = 0;
        base.BitPosition = 8;
    }
}
