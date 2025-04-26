/*
Sourced directly from the Kronos project.
*/

#if NET7_0_OR_GREATER

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using ArmCrc = System.Runtime.Intrinsics.Arm.Crc32;

namespace Imlight.CoreLib.Shared.Cryptography;

internal sealed partial class Crc32 {
    
    private const uint DEFAULT_INITIAL_STATE = 0;
    private const ulong K1 = 0x154442bd4;
    private const ulong K2 = 0x1c6e41596;
    private const ulong K3 = 0x1751997d0;
    private const ulong K4 = 0x0ccaa009e;
    private const ulong K5 = 0x163cd6124;

    private const ulong POLY_X = 0x1DB710641;
    private const ulong PRIME = 0x1F7011641;

    // static uint[] GenerateTable(uint poly)
    // {
    //     uint[] table = new uint[256];
    //     for (int i = 0; i < table.Length; i++)
    //     {
    //         uint val = (uint)i;
    //         for (int j = 0; j < 8; j++)
    //         {
    //             if ((val & 0b0000_0001) == 0)
    //             {
    //                 val >>= 1;
    //             }
    //             else
    //             {
    //                 val = (val >> 1) ^ poly;
    //             }
    //         }
    //         table[i] = val;
    //     }
    //     return table;
    // }
    //
    // GenerateTable(0x04C11DB7);

    private static ReadOnlySpan<uint> LookupTable => [
        0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3, 0x0EDB8832,
        0x79DCB8A4, 0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91, 0x1DB71064, 0x6AB020F2,
        0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7, 0x136C9856, 0x646BA8C0, 0xFD62F97A,
        0x8A65C9EC, 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5, 0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172,
        0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B, 0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3,
        0x45DF5C75, 0xDCD60DCF, 0xABD13D59, 0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423,
        0xCFBA9599, 0xB8BDA50F, 0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924, 0x2F6F7C87, 0x58684C11, 0xC1611DAB,
        0xB6662D3D, 0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
        0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01, 0x6B6B51F4,
        0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457, 0x65B0D9C6, 0x12B7E950,
        0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65, 0x4DB26158, 0x3AB551CE, 0xA3BC0074,
        0xD4BB30E2, 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB, 0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0,
        0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9, 0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086, 0x5768B525,
        0x206F85B3, 0xB966D409, 0xCE61E49F, 0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81,
        0xB7BD5C3B, 0xC0BA6CAD, 0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615,
        0x73DC1683, 0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
        0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7, 0xFED41B76,
        0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5, 0xD6D6A3E8, 0xA1D1937E,
        0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B, 0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6,
        0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79, 0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236,
        0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F, 0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7,
        0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D, 0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F,
        0x72076785, 0x05005713, 0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7,
        0x0BDBDF21, 0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
        0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45, 0xA00AE278,
        0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB, 0xAED16A4A, 0xD9D65ADC,
        0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9, 0xBDBDF21C, 0xCABAC28A, 0x53B39330,
        0x24B4A3A6, 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF, 0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94,
        0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
    ];

    public uint Hash { get; private set; }

    // We can technically accelerate smaller chunks too but it's too much hassle for too
    // little benefit. We are falling back to the slow software implementation in this case.
    // NOTE: Pclmulqdq implies SSE2 support.
    private static bool CanVectorize(ReadOnlySpan<byte> source) =>
        Pclmulqdq.IsSupported && source.Length >= Vector128<byte>.Count * 8;

    public Crc32() {
        Hash = DEFAULT_INITIAL_STATE;
    }

    public Crc32(uint initial) {
        Hash = initial;
    }

    public void Update(ReadOnlySpan<byte> source) {
        Hash = Calculate(Hash, source);
    }

    public void Reset() {
        Hash = DEFAULT_INITIAL_STATE;
    }

    public static uint GetHash(uint initial, ReadOnlySpan<byte> source) {
        return Calculate(initial, source);
    }

    public static uint GetHash(ReadOnlySpan<byte> source) {
        return Calculate(DEFAULT_INITIAL_STATE, source);
    }

    private static uint Calculate(uint crc, ReadOnlySpan<byte> source) {
        if (CanVectorize(source)) {
            return CalculateVectorized(crc, source);
        }

        if (ArmCrc.Arm64.IsSupported) {
            return CalculateArm64(crc, source);
        }

        if (ArmCrc.IsSupported) {
            return CalculateArm32(crc, source);
        }

        return CalculateSlow(crc, source);
    }

    private static uint CalculateSlow(uint crc, ReadOnlySpan<byte> source) {
        crc = ~crc;

        var crcLookup = LookupTable;
        for (var i = 0; i < source.Length; ++i) {
            var idx = (byte) crc;
            idx ^= source[i];

            crc = crcLookup[idx] ^ crc >> 8;
        }

        return ~crc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ulong> Reduce128(Vector128<ulong> dest, Vector128<ulong> source, Vector128<ulong> keys) {
        dest ^= Pclmulqdq.CarrylessMultiply(source, keys, 0x00);
        dest ^= Pclmulqdq.CarrylessMultiply(source, keys, 0x11);

        return dest;
    }

    // Based on the algorithm described in "Fast CRC Computation for Generic Polynomials Using
    // PCLMULQDQ Instruction" in December, 2009.
    private static uint CalculateVectorized(uint crc, ReadOnlySpan<byte> source) {
        Debug.Assert(CanVectorize(source));

        ref var sourceRef = ref MemoryMarshal.GetReference(source);
        var sourceLen = source.Length;

        // Step 1: Fold by 4 loop.
        var x3 = Vector128.LoadUnsafe(ref sourceRef).AsUInt64();
        var x2 = Vector128.LoadUnsafe(ref sourceRef, 16).AsUInt64();
        var x1 = Vector128.LoadUnsafe(ref sourceRef, 32).AsUInt64();
        var x0 = Vector128.LoadUnsafe(ref sourceRef, 48).AsUInt64();

        sourceRef = ref Unsafe.Add(ref sourceRef, Vector128<byte>.Count * 4);
        sourceLen -= Vector128<byte>.Count * 4;

        // Fold in the initial state value as part of incremental CRC checksums.
        x3 ^= Vector128.CreateScalar(~crc).AsUInt64();

        // Fold blocks of 64 in parallel, if any.
        var keys = Vector128.Create(K1, K2);
        while (sourceLen >= Vector128<byte>.Count * 4) {
            var y3 = Vector128.LoadUnsafe(ref sourceRef).AsUInt64();
            var y2 = Vector128.LoadUnsafe(ref sourceRef, 16).AsUInt64();
            var y1 = Vector128.LoadUnsafe(ref sourceRef, 32).AsUInt64();
            var y0 = Vector128.LoadUnsafe(ref sourceRef, 48).AsUInt64();

            x3 = Reduce128(y3, x3, keys);
            x2 = Reduce128(y2, x2, keys);
            x1 = Reduce128(y1, x1, keys);
            x0 = Reduce128(y0, x0, keys);

            sourceRef = ref Unsafe.Add(ref sourceRef, Vector128<byte>.Count * 4);
            sourceLen -= Vector128<byte>.Count * 4;
        }

        // Fold into 128 bits.
        keys = Vector128.Create(K3, K4);
        var x = Reduce128(x2, x3, keys);
        x = Reduce128(x1, x, keys);
        x = Reduce128(x0, x, keys);

        // Step 2: Single fold blocks by one.
        while (sourceLen >= Vector128<byte>.Count) {
            x = Reduce128(Vector128.LoadUnsafe(ref sourceRef).AsUInt64(), x, keys);

            sourceRef = ref Unsafe.Add(ref sourceRef, Vector128<byte>.Count);
            sourceLen -= Vector128<byte>.Count;
        }

        // Step 3: Reduction from 128 bits to 64 bits.
        var bitmask = Vector128.Create(~0, 0, 0, 0).AsUInt64();
        x = Pclmulqdq.CarrylessMultiply(x, keys, 0x10) ^ Sse2.ShiftRightLogical128BitLane(x, 8);
        x = Pclmulqdq.CarrylessMultiply(x & bitmask, Vector128.CreateScalar(K5), 0x00) ^
            Sse2.ShiftRightLogical128BitLane(x, 4);

        // Perform a Barret reduction from our now 64 bits to 32 bits.
        var pu = Vector128.Create(POLY_X, PRIME);
        // T1(x) = ⌊(R(x) % x^32)⌋ • μ
        var t1 = Pclmulqdq.CarrylessMultiply(x & bitmask, pu, 0x10);
        // T2(x) = ⌊(T1(x) % x^32)⌋ • P(x)
        var t2 = Pclmulqdq.CarrylessMultiply(t1 & bitmask, pu, 0x00);
        // Since we're doing the bit-reflected variant, we fetch the upper 32 bits.
        //
        // C(x) = R(x) ^ T2(x) / x^32
        x ^= t2;
        var c = x.AsUInt32().GetElement(1);

        // For remainders smaller than one block, use the slow path.
        return sourceLen > 0
            ? CalculateSlow(~c, MemoryMarshal.CreateReadOnlySpan(ref sourceRef, sourceLen))
            : ~c;
    }

    private static uint CalculateArm64(uint crc, ReadOnlySpan<byte> source) {
        Debug.Assert(ArmCrc.Arm64.IsSupported);

        if (source.Length >= sizeof(ulong)) {
            // We want to process the source input in 8 byte chunks.
            // We split remaining bytes off the end and do those separately.
            ref byte ptr = ref MemoryMarshal.GetReference(source);
            var alignedLen = BitUtils.AlignDown(source.Length, 8);

            for (var i = 0; i < alignedLen; i += sizeof(ulong)) {
                crc = ArmCrc.Arm64.ComputeCrc32(crc, Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref ptr, i)));
            }

            source = source[alignedLen..];
        }

        // Compute remaining individual bytes.
        foreach (var b in source) {
            crc = ArmCrc.ComputeCrc32(crc, b);
        }

        return ~crc;
    }

    private static uint CalculateArm32(uint crc, ReadOnlySpan<byte> source) {
        Debug.Assert(ArmCrc.IsSupported);

        if (source.Length >= sizeof(uint)) {
            // We want to process the source input in 4 byte chunks.
            // We split remaining bytes off the end and do those separately.
            ref byte ptr = ref MemoryMarshal.GetReference(source);
            var alignedLen = BitUtils.AlignDown(source.Length, 4);

            for (var i = 0; i < alignedLen; i += sizeof(ulong)) {
                crc = ArmCrc.ComputeCrc32(crc, Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref ptr, i)));
            }

            source = source[alignedLen..];
        }

        // Compute remaining individual bytes.
        foreach (var b in source) {
            crc = ArmCrc.ComputeCrc32(crc, b);
        }

        return ~crc;
    }

}

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
