/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
*/

#if NET7_0_OR_GREATER

using System.Runtime.CompilerServices;

namespace Imlight.CoreLib.Shared.Cryptography;

public static class BitUtils {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignUp(int value, int align) 
        => AlignDown(value + (align - 1), align);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AlignUp(uint value, int align) 
        => AlignDown(value + (uint) (align - 1), align);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignDown(int value, int align) 
        => (int) AlignDown((uint) value, align);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint AlignDown(uint value, int align) 
        => value & ~(uint) (align - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SignExtend(uint value, int nbits) {
        var shift = 32 - nbits;
        return (int) (value << shift) >> shift;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SignExtend(ulong value, int nbits) {
        var shift = 64 - nbits;
        return (long) (value << shift) >> shift;
    }

}

#endif
