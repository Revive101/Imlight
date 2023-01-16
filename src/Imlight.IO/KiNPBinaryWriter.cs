using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Imlight.IO
{
    /// <summary>
    /// Inherits BinaryWriter, and provides extra functionality to craft Kingsisle binary structures.
    /// </summary>
    internal class KiNPBinaryWriter : BinaryWriter, IDisposable
    {
        internal KiNPBinaryWriter()
        {
            base.OutStream = new MemoryStream();
        }

        internal void WriteBYT(byte value) => base.Write(value);
        internal void WriteSBYT(sbyte value) => base.Write(value);
        internal void WriteSHRT(short value) => base.Write(value);
        internal void WriteUSHRT(ushort value) => base.Write(value);
        internal void WriteINT(int value) => base.Write(value);
        internal void WriteUINT(uint value) => base.Write(value);
        internal void WriteFLT(float value) => base.Write((float)value);
        internal void WriteDBL(double value) => base.Write((double)value);
        internal void WriteGID(ulong value) => base.Write(value);

        internal void WriteSTR(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            base.Write((int)bytes.Length);
            base.Write(bytes);
        }

        internal void WriteWSTR(string value)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(value);
            base.Write(((int)bytes.Length));
            base.Write(bytes);
        }

        internal void WriteMagicHeader() => WriteUSHRT(0xF00D);

        internal byte[] GetBytes()
        {
            using var memStream = new MemoryStream();
            var oldPos = base.OutStream.Position;
            base.OutStream.Position = 0;
            base.OutStream.CopyTo(memStream);
            base.OutStream.Position = oldPos;
            return memStream.ToArray();
        }

        public new void Dispose()
        {
            base.Close();
            base.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
