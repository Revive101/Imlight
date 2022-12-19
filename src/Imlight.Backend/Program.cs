using System;
using Imlight.Realm;
using Imlight.Engine;
using Imlight.Common;

namespace Imlight.Backend
{
    internal class Program
    {

        internal static RealmManager RealmManager { get; private set; }

        static void Main(string[] args)
        {
            // Processor test
            ProcessorManager.StartNewProcessor();

            // Realm test
            RealmManager = new RealmManager();
            RealmManager.CreateRealm("test realm");

            Console.ReadKey();
        }

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
    }
}
