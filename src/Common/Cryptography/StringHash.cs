/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.Common.Cryptography 
{
    public static class StringHash
    {
        public static uint Compute(string input)
        {
            int result = 0;

            var shift1 = 0;
            var shift2 = 32;
            foreach (char c in input)
            {
                var cb = (byte)c;

                result ^= (cb - 32) << shift1;

                if (shift1 > 24)
                {
                    result ^= (cb - 32) >> shift2;
                    if (shift1 >= 27)
                    {
                        shift1 -= 32;
                        shift2 += 32;
                    }
                }
                shift1 += 5;
                shift2 -= 5;
            }

            if (result < 0)
                result = -result;

            return (uint)result;
        }
    }
}