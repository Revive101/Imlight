/*
BSD 3-Clause License

Copyright (c) 2024, Jooty

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.
*/

using System;
using System.Runtime.InteropServices;

namespace Imlight.Common.ObjectProperty.PropertyReflection;

[StructLayout(LayoutKind.Explicit)]
public struct GID(ulong full) : IConvertible {
    [FieldOffset(0)]
    public ulong Full = full;

    [FieldOffset(0)]
    public PartsStruct MParts;

    public ulong Value {
        readonly get => Full;
        set => Full = value;
    }

    public struct PartsStruct {
        public uint Id;
        public byte Block;
        public byte Type;
        public ushort ServerProc;
    }

    public static explicit operator GID(ulong value) => new(value);
    public static implicit operator ulong(GID gid) => gid.Full;

    public readonly TypeCode GetTypeCode() => TypeCode.UInt64;
    public readonly bool ToBoolean(IFormatProvider? provider) => Full != 0;
    public readonly char ToChar(IFormatProvider? provider) => (char) Full;
    public readonly sbyte ToSByte(IFormatProvider? provider) => (sbyte) Full;
    public readonly byte ToByte(IFormatProvider? provider) => (byte) Full;
    public readonly short ToInt16(IFormatProvider? provider) => (short) Full;
    public readonly ushort ToUInt16(IFormatProvider? provider) => (ushort) Full;
    public readonly int ToInt32(IFormatProvider? provider) => (int) Full;
    public readonly uint ToUInt32(IFormatProvider? provider) => (uint) Full;
    public readonly long ToInt64(IFormatProvider? provider) => (long) Full;
    public readonly ulong ToUInt64(IFormatProvider? provider) => Full;
    public readonly float ToSingle(IFormatProvider? provider) => Full;
    public readonly double ToDouble(IFormatProvider? provider) => Full;
    public readonly decimal ToDecimal(IFormatProvider? provider) => Full;
    public readonly DateTime ToDateTime(IFormatProvider? provider) => throw new InvalidCastException();
    public readonly string ToString(IFormatProvider? provider) => Full.ToString();
    public readonly object ToType(Type conversionType, IFormatProvider? provider) => Convert.ChangeType(Full, conversionType);

}