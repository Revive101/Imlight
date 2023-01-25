using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Internals
{
    public struct ULongWord : IConvertible
    {
        private int _value;

        public int Value { get => _value; set => _value = value; }
        public ULongWord(int value) => _value = value;

        public static explicit operator ULongWord(int value) => new ULongWord(value);
        public static implicit operator int(ULongWord bits) => bits._value;

        public TypeCode GetTypeCode() => TypeCode.Byte;
        public bool ToBoolean(IFormatProvider provider) => _value != 0;
        public char ToChar(IFormatProvider provider) => (char)_value;
        public sbyte ToSByte(IFormatProvider provider) => (sbyte)_value;
        public byte ToByte(IFormatProvider provider) => (byte)_value;
        public short ToInt16(IFormatProvider provider) => (short)_value;
        public ushort ToUInt16(IFormatProvider provider) => (ushort)_value;
        public int ToInt32(IFormatProvider provider) => (int)_value;
        public uint ToUInt32(IFormatProvider provider) => (uint)_value;
        public long ToInt64(IFormatProvider provider) => (long)_value;
        public ulong ToUInt64(IFormatProvider provider) => (ulong)_value;
        public float ToSingle(IFormatProvider provider) => _value;
        public double ToDouble(IFormatProvider provider) => _value;
        public decimal ToDecimal(IFormatProvider provider) => _value;
        public DateTime ToDateTime(IFormatProvider provider) => throw new InvalidCastException();
        public string ToString(IFormatProvider provider) => _value.ToString();
        public object ToType(Type conversionType, IFormatProvider provider) => Convert.ChangeType(_value, conversionType);
    }
}
