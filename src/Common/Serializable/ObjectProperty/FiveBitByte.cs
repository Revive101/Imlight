/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.Common.Serializable.ObjectProperty.JSON;
using Newtonsoft.Json;

namespace Imlight.Common.Serializable.ObjectProperty;

[JsonConverter(typeof(FiveBitByteConverter))]
public struct FiveBitByte : IConvertible
{
    private byte _value;

    public byte Value { get => _value; set => _value = value; }
    public FiveBitByte(byte value) => _value = value;

    public static explicit operator FiveBitByte(byte value) => new FiveBitByte(value);
    public static implicit operator byte(FiveBitByte bits) => bits._value;

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
    public ulong ToUInt64(IFormatProvider provider) => _value;
    public float ToSingle(IFormatProvider provider) => _value;
    public double ToDouble(IFormatProvider provider) => _value;
    public decimal ToDecimal(IFormatProvider provider) => _value;
    public DateTime ToDateTime(IFormatProvider provider) => throw new InvalidCastException();
    public string ToString(IFormatProvider provider) => _value.ToString();
    public object ToType(Type conversionType, IFormatProvider provider) => Convert.ChangeType(_value, conversionType);
}