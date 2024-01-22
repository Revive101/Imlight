/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

﻿using Imlight.Common.ObjectProperty.JSON;
using Newtonsoft.Json;
using System;

namespace Imlight.Common.ObjectProperty.PropertyReflection;

[JsonConverter(typeof(TwoBitByteConverter))]
public struct Bui2 : IConvertible {
    private byte _value;

    public byte Value { readonly get => _value; set => _value = value; }
    public Bui2(byte value) => _value = value;

    public static explicit operator Bui2(byte value) => new Bui2(value);
    public static implicit operator byte(Bui2 bits) => bits._value;

    public readonly TypeCode GetTypeCode() => TypeCode.Byte;
    public readonly bool ToBoolean(IFormatProvider? provider) => _value != 0;
    public readonly char ToChar(IFormatProvider? provider) => (char)_value;
    public readonly sbyte ToSByte(IFormatProvider? provider) => (sbyte)_value;
    public readonly byte ToByte(IFormatProvider? provider) => (byte)_value;
    public readonly short ToInt16(IFormatProvider? provider) => (short)_value;
    public readonly ushort ToUInt16(IFormatProvider? provider) => (ushort)_value;
    public readonly int ToInt32(IFormatProvider? provider) => (int)_value;
    public readonly uint ToUInt32(IFormatProvider? provider) => (uint)_value;
    public readonly long ToInt64(IFormatProvider? provider) => (long)_value;
    public readonly ulong ToUInt64(IFormatProvider? provider) => _value;
    public readonly float ToSingle(IFormatProvider? provider) => _value;
    public readonly double ToDouble(IFormatProvider? provider) => _value;
    public readonly decimal ToDecimal(IFormatProvider? provider) => _value;
    public readonly DateTime ToDateTime(IFormatProvider? provider) => throw new InvalidCastException();
    public readonly string ToString(IFormatProvider? provider) => _value.ToString();
    public readonly object ToType(Type conversionType, IFormatProvider? provider) => Convert.ChangeType(_value, conversionType);
}
