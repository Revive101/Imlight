using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System.Text;
using WizUnraveler;

namespace Imlight.Common.Cryptography
{
    public static class Rec1
    {
        private const byte TWOFISH_BLOCK_SIZE = 0x10;
        private const byte TWOFISH_KEY_SIZE = 2 * TWOFISH_BLOCK_SIZE;
        private const byte TWOFISH_NONCE_SIZE = TWOFISH_BLOCK_SIZE;

        private const byte KEY_CONSTANT = 0x17;
        private const byte IV_CONSTANT = 0xB6;

        public static ByteString Encode(ushort sid, string username, string clientKey, uint timeSecs, uint timeMillis)
        {
            byte[] record = Encoding.UTF8.GetBytes($"{sid} {username} {clientKey}");

            byte[] key = DeriveTwofishKey(sid, timeSecs, timeMillis);
            byte[] nonce = DeriveTwofishNonce();

            IBufferedCipher cipher = CipherUtilities.GetCipher("Twofish/OFB/NoPadding");
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), nonce));
            return cipher.DoFinal(record);
        }

        public static ByteString Decode(byte[] encodedData, ushort sid, uint timeSecs, uint timeMillis)
        {
            byte[] key = DeriveTwofishKey(sid, timeSecs, timeMillis);
            byte[] nonce = DeriveTwofishNonce();

            IBufferedCipher cipher = CipherUtilities.GetCipher("Twofish/OFB/NoPadding");
            cipher.Init(false, new ParametersWithIV(new KeyParameter(key), nonce));
            return cipher.DoFinal(encodedData);
        }

        private static byte[] DeriveTwofishKey(ushort sessionID, uint timeSecs, uint timeMillis)
        {
            byte[] key = new byte[TWOFISH_KEY_SIZE];

            for (int i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(KEY_CONSTANT + i);
            }

            key[4]  = (byte)(sessionID & 0xff);
            key[5]  = 0;
            key[6]  = (byte)((sessionID >> 8) & 0xff);

            key[8]  = (byte)(timeSecs & 0xff);
            key[9]  = (byte)((timeSecs >> 16) & 0xff);
            key[12] = (byte)((timeSecs >> 8)  & 0xff);
            key[13] = (byte)((timeSecs >> 24) & 0xff);

            key[14] = (byte)(timeMillis & 0xff);
            key[15] = (byte)((timeMillis >> 8) & 0xff);

            return key;
        }

        private static byte[] DeriveTwofishNonce()
        {
            var iv = new byte[TWOFISH_NONCE_SIZE];

            for (int i = 0; i < iv.Length; i++)
            {
                iv[i] = (byte)(IV_CONSTANT - i);
            }

            return iv;
        }
    }
}
