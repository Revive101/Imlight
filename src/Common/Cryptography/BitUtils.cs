#if NET7_0_OR_GREATER

using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Imlight.Common.Cryptography;

public static class BitUtils {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignUp(int value, int align) {
        return AlignDown(value + (align - 1), align);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AlignUp(uint value, int align) {
        return AlignDown(value + (uint) (align - 1), align);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignDown(int value, int align) {
        return (int) AlignDown((uint) value, align);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AlignDown(uint value, int align) {
        Debug.Assert(BitOperations.IsPow2(align));
        return value & ~(uint) (align - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SignExtend(uint value, int nbits) {
        Debug.Assert(nbits is > 0 and < 32);

        var shift = 32 - nbits;
        return (int) (value << shift) >> shift;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SignExtend(ulong value, int nbits) {
        Debug.Assert(nbits is > 0 and < 64);

        var shift = 64 - nbits;
        return (long) (value << shift) >> shift;
    }
}

#endif
