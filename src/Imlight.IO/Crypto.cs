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
            uint result = 0;
            var shift1 = 0;
            for (int i = 0; i < input.Length; i++)
            {
                var cb = BitConverter.ToUInt32(BitConverter.GetBytes(input[i]), 0);
                result ^= (cb - 32) << shift1;
                if (shift1 >= 27)
                    shift1 -= 32;
                shift1 += 5;
            }
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
