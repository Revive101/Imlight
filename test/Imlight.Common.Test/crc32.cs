using NUnit.Framework;
using Imlight.Common.Cryptography;
using static Nito.HashAlgorithms.CRC32;

namespace Imlight.Common.Test
{
    [TestFixture]
    public class crc32Tests
    {
        [Test(ExpectedResult = "FE3D4A0A")]
        public string crc32_nito() 
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(@"KIWAD");
            var definition = new Definition
            {
                Initializer                 = 0xFFFFFFFF,
                TruncatedPolynomial         = 0x04C11DB7,
                FinalXorValue               = 0xFFFFFFFF,
                ReverseResultBeforeFinalXor = true,
                ReverseDataBytes            = true
            };

            var crcengine = new Nito.HashAlgorithms.CRC32(definition);
            var crc32 = crcengine.ComputeHash(bytes);
            Array.Reverse(crc32);

            return Convert.ToHexString(crc32);
        }
    }
}
