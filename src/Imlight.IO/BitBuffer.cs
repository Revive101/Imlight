using System;
using System.Diagnostics;
using SharpDX;
using System.IO;
using System.Text;


namespace Imlight.IO
{
    public class BitBuffer : IDisposable
    {
        private byte _bitPosition = 8;
        private byte BitValue;
        private BinaryWriter _writeStream;
        private BinaryReader _readStream;

        // ctor
        public BitBuffer()
        {
            _writeStream = new BinaryWriter(new MemoryStream());
        }

        // ctor
        public BitBuffer(byte[] data)
        {
            _readStream = new BinaryReader(new MemoryStream(data));
        }

        // ctor
        public BitBuffer(byte[] data, int length)
        {
            _readStream = new BinaryReader(new MemoryStream(data, 0, length));
        }

        // ctor
        public BitBuffer(Stream stream)
        {
            _readStream = new BinaryReader(stream);
        }

        #region Read Methods

        public sbyte ReadInt8()
        {
            ResetBitPos();
            return _readStream.ReadSByte();
        }

        public short ReadInt16()
        {
            ResetBitPos();
            return _readStream.ReadInt16();
        }

        public int ReadInt32()
        {
            ResetBitPos();
            return _readStream.ReadInt32();
        }

        public long ReadInt64()
        {
            ResetBitPos();
            return _readStream.ReadInt64();
        }

        public byte ReadUInt8()
        {
            ResetBitPos();
            return _readStream.ReadByte();
        }

        public ushort ReadUInt16()
        {
            ResetBitPos();
            return _readStream.ReadUInt16();
        }

        public uint ReadUInt32()
        {
            ResetBitPos();
            return _readStream.ReadUInt32();
        }

        public ulong ReadUInt64()
        {
            ResetBitPos();
            return _readStream.ReadUInt64();
        }

        public float ReadFloat()
        {
            ResetBitPos();
            return _readStream.ReadSingle();
        }

        public double ReadDouble()
        {
            ResetBitPos();
            return _readStream.ReadDouble();
        }

        public string ReadCString()
        {
            ResetBitPos();
            string tmpString = string.Empty;
            char tmpChar = _readStream.ReadChar();
            char tmpEndChar = Convert.ToChar(Encoding.ASCII.GetString(new byte[] { 0 }));

            while (tmpChar != tmpEndChar)
            {
                tmpString += tmpChar;
                tmpChar = _readStream.ReadChar();
            }

            return tmpString;
        }

        public string ReadString()
        {
            var length = _readStream.ReadUInt16();

            ResetBitPos();
            return Encoding.UTF8.GetString(ReadBytes(length));
        }

        public bool ReadBool()
        {
            ResetBitPos();
            return _readStream.ReadBoolean();
        }

        public byte[] ReadBytes(int count)
        {
            ResetBitPos();
            return _readStream.ReadBytes(count);
        }

        public Vector2 ReadVector2()
        {
            ResetBitPos();
            return new Vector2(
                _readStream.ReadSingle(),
                _readStream.ReadSingle());
        }

        public Vector3 ReadVector3()
        {
            ResetBitPos();
            return new Vector3(
                _readStream.ReadSingle(), 
                _readStream.ReadSingle(), 
                _readStream.ReadSingle());
        }

        public Quaternion ReadQuaternion()
        {
            ResetBitPos();
            return new Quaternion(
                _readStream.ReadSingle(), 
                _readStream.ReadSingle(), 
                _readStream.ReadSingle(), 
                _readStream.ReadSingle());
        }

        public Matrix ReadMatrix()
        {
            ResetBitPos();
            var m = new float[16];
            for (int i = 0; i < 16; i++)
            {
                m[i] = _readStream.ReadSingle();
            }

            return new Matrix(m);
        }

        public Color ReadColor()
        {
            ResetBitPos();
            return new Color(
                _readStream.ReadByte(),
                _readStream.ReadByte(),
                _readStream.ReadByte());
        }

        public Rectangle ReadRectangle()
        {
            ResetBitPos();
            return new Rectangle(
                _readStream.ReadInt32(),
                _readStream.ReadInt32(),
                _readStream.ReadInt32(),
                _readStream.ReadInt32());
        }

        public RectangleF ReadRectangleF()
        {
            ResetBitPos();
            return new RectangleF(
                _readStream.ReadSingle(),
                _readStream.ReadSingle(),
                _readStream.ReadSingle(),
                _readStream.ReadSingle());
        }

        //BitPacking
        public bool ReadBit()
        {
            if (_bitPosition == 8)
            {
                try
                {
                    BitValue = Reverse(ReadUInt8());
                }
                catch (EndOfStreamException)
                {
                    BitValue = 0; // TOOD: HOW ALLOWED IS THIS
                }
                _bitPosition = 0;
            }

            int returnValue = BitValue;
            BitValue = (byte)(2 * returnValue);
            ++_bitPosition;

            return ((byte)(returnValue >> 7)) != 0;
            //return (byte)(returnValue & 1); // todo: CHANGED TO INVERT BIT ORDER
        }

        public int TellBitPos()
        {
            if (_readStream != null)
            {
                var offset = (int)GetCurrentStream().Position - (_bitPosition != 0 ? 1 : 0);
                return _bitPosition + 8 * offset;
            }
            return 8 - _bitPosition + 8 * (int)GetCurrentStream().Position;
        }

        public byte CurrentByteBitPos => _bitPosition;

        #endregion

        #region Write Methods

        public void WriteInt8(sbyte data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteInt16(short data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteInt32(int data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteInt64(long data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteUInt8(byte data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteUInt16(ushort data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteUInt32(uint data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteUInt64(ulong data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteFloat(float data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        public void WriteDouble(double data)
        {
            FlushBits();
            _writeStream.Write(data);
        }

        /// <summary>
        /// Writes a string to the packet with a null terminated (0)
        /// </summary>
        /// <param name="str"></param>
        public void WriteCString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                WriteUInt8(0);
                return;
            }

            WriteString(str);
            WriteUInt8(0);
        }
        public void WriteColor(Color col)
        {
            byte[] color = new byte[4] { col.R, col.G, col.B, col.A };
            _writeStream.Write(color);
        }
        public void WriteString(string str)
        {
            if (string.IsNullOrEmpty(str))
                return;

            byte[] sBytes = Encoding.UTF8.GetBytes(str);
            WriteBytes(sBytes);
        }

        public void WriteBytes(byte[] data)
        {
            FlushBits();
            _writeStream.Write(data, 0, data.Length);
        }

        public void WriteBytes(byte[] data, uint count)
        {
            FlushBits();
            _writeStream.Write(data, 0, (int)count);
        }

        public void WriteBytes(BitBuffer buffer)
        {
            WriteBytes(buffer.GetData());
        }

        /*public void WriteVector3(teVec3 pos)
        {
            WriteFloat(pos.X);
            WriteFloat(pos.Y);
            WriteFloat(pos.Z);
        }

        public void WriteVector2(Vector2 pos)
        {
            WriteFloat(pos.X);
            WriteFloat(pos.Y);
        }

        public void WritePackXYZ(Vector3 pos)
        {
            uint packed = 0;
            packed |= ((uint)(pos.X / 0.25f) & 0x7FF);
            packed |= ((uint)(pos.Y / 0.25f) & 0x7FF) << 11;
            packed |= ((uint)(pos.Z / 0.25f) & 0x3FF) << 22;
            WriteUInt32(packed);
        }*/

        public static byte Reverse(byte b)
        {
            int a = 0;
            for (int i = 0; i < 8; i++)
                if ((b & (1 << i)) != 0)
                    a |= 1 << (7 - i);
            return (byte)a;
        }

        public void WriteBit(bool bit)
        {
            WriteBit(bit ? (byte)1 : (byte)0);
        }

        public void WriteBit(byte bit)
        {
            Debug.Assert(bit == 0 || bit == 1);

            --_bitPosition;

            if (bit == 1)
                BitValue |= (byte)(1 << _bitPosition);

            if (_bitPosition == 0) FlushBits();
        }

        public unsafe T ReadBits<T>(int bitCount) where T : unmanaged
        {
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

        public void FlushBits()
        {
            Debug.Assert(_readStream == null);
            Debug.Assert(_writeStream != null);

            if (_bitPosition == 8)
                return;

            _writeStream.Write(Reverse(BitValue));
            BitValue = 0;
            _bitPosition = 8;
        }

        public void ResetBitPos()
        {
            Debug.Assert(_writeStream == null);


            if (_bitPosition > 7)
                return;

            _bitPosition = 8;
            BitValue = 0;
        }

        public byte[] GetData()
        {
            Stream stream = GetCurrentStream();

            byte[] data = new byte[stream.Length];

            long pos = stream.Position;
            stream.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)stream.ReadByte();

            stream.Seek(pos, SeekOrigin.Begin);
            return data;
        }

        public uint GetSize()
        {
            return (uint)GetCurrentStream().Length;
        }

        public Stream GetCurrentStream()
        {
            return _writeStream != null ? _writeStream.BaseStream : _readStream.BaseStream;
        }

        public void Clear()
        {
            _bitPosition = 8;
            BitValue = 0;
            _writeStream = new BinaryWriter(new MemoryStream());
        }

        public void SwapToRead()
        {
            Debug.Assert(_writeStream != null && _readStream == null);

            _readStream?.Dispose();
            _readStream = new BinaryReader(_writeStream.BaseStream);
            _writeStream = null;
        }

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
    }
}