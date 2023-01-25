using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.IO
{
    public static class Crypto
    {
        public static uint HashString(string input)
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

        public static uint HashPropertyName(string name, string type)
        {
            uint typeHash = HashString(type);
            var propHash = Djb2Hash(name) & 0x7FFF_FFFF;

            // Dropping the most-significant byte.
            return (typeHash + propHash) & 0xFFFF_FFFF;
        }

        public static uint Djb2Hash(string str)
        {
            uint hash = 5381;

            for (int i = 0; i < str.Length; i++)
            {
                hash = ((hash << 5) + hash) + ((byte)str[(int)i]);
            }

            return hash;
        }
    }
}
