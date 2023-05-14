using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Imlight.Common.Cryptography
{
    public static class ClientKey
    {
        /// <summary>
        /// Constructs a new salted ClientKey1 hash.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sessionID"></param>
        /// <param name="timeSecs"></param>
        /// <param name="timeMillis"></param>
        /// <returns></returns>
        public static string EncodeCK1(string input, ushort sessionID, uint timeSecs, uint timeMillis)
        {
            var passwordHash = HashPassword(input);

            var salt = $"{sessionID}{timeSecs}{timeMillis}";
            return SecondaryEncrypt(passwordHash, salt);
        }
        
        /// <summary>
        /// Verify a ClientKey1 hash against an input.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sessionID"></param>
        /// <param name="timeSecs"></param>
        /// <param name="timeMillis"></param>
        /// <param name="encodedString"></param>
        /// <returns></returns>
        public static bool VerifyCK1(string input, ushort sessionID, uint timeSecs, uint timeMillis, string encodedString)
        {
            var expectedEncodedString = EncodeCK1(input, sessionID, timeSecs, timeMillis);
            return encodedString == expectedEncodedString;
        }

        /// <summary>
        /// Constructs a new salted ClientKey2 hash. This is used to validate a player's game client after their patcher closes.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="sessionID"></param>
        /// <param name="timeSecs"></param>
        /// <param name="timeMillis"></param>
        /// <returns></returns>
        public static string EncodeCK2(ushort sessionID, uint timeSecs, uint timeMillis)
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                byte[] tokenData = new byte[32];
                rng.GetBytes(tokenData);

                string sessionHash = Convert.ToBase64String(tokenData);
                string salt = $"{timeMillis}{timeSecs}{sessionID}";

                return SecondaryEncrypt(sessionHash, salt);
            }
        }

        private static string HashPassword(string password)
        {
            using var sha512 = SHA512.Create();
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

            return Convert.ToBase64String(sha512.ComputeHash(passwordBytes));
        }

        public static string SecondaryEncrypt(string password, string seed)
        {
            using var sha512 = SHA512.Create();
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] seedBytes = Encoding.UTF8.GetBytes(seed);

            byte[] hash = sha512.ComputeHash(passwordBytes.Concat(seedBytes).ToArray());

            return Convert.ToBase64String(hash);
        }
    }
}
