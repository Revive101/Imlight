using System;
using static Nito.HashAlgorithms.CRC32;

namespace Imlight.Common.Cryptography
{
    public static class crc32
    {
        private const uint SEED = 0xFFFFFFFF;
        private const uint POLY = 0x04C11DB7;
        private const uint FINAL_XOR = 0xFFFFFFFF;

        public static uint Compute(byte[] input)
        {
            var definition = new Definition
            {
                Initializer                 = SEED,
                TruncatedPolynomial         = POLY,
                FinalXorValue               = FINAL_XOR,
                ReverseResultBeforeFinalXor = true,
                ReverseDataBytes            = true
            };

            var crcengine = new Nito.HashAlgorithms.CRC32(definition);
            var crc32 = crcengine.ComputeHash(input);
            Array.Reverse(crc32);

            return BitConverter.ToUInt32(crc32);
        }
    }
}
