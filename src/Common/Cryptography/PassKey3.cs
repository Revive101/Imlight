/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Imlight.Common.Cryptography
{
    public static class PassKey3
    {
        public static string EncodePK3(string input, ushort sessionID, uint timeSecs, uint timeMillis)
        {
            using var sha512 = SHA512.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            sha512.TransformBlock(bytes, 0, bytes.Length, null, 0);

            var sessionInfoBytes = Encoding.UTF8.GetBytes($"{sessionID}{timeSecs}{timeMillis}");
            sha512.TransformFinalBlock(sessionInfoBytes, 0, sessionInfoBytes.Length);

            var passkey3Bytes = sha512.Hash;
            var passkey3 = Convert.ToBase64String(passkey3Bytes);

            return passkey3;
        }

        public static bool VerifyPK3(string input, ushort sessionID, uint timeSecs, uint timeMillis, string encodedString)
        {
            var expectedEncodedString = EncodePK3(input, sessionID, timeSecs, timeMillis);
            return encodedString == expectedEncodedString;
        }
    }
}
