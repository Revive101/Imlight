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
            // Convert input string to bytes so we can actually work with it.
            var encoding = Encoding.UTF8;
            var inputBytes = encoding.GetBytes(input);

            using (var sha512 = SHA512.Create())
            {
                var firstHash = sha512.ComputeHash(inputBytes);

                // Add salt to our soup!
                var salt = encoding.GetBytes($"{sessionID}{timeSecs}{timeMillis}");
                var state = sha512.ComputeHash(firstHash.Concat(salt).ToArray());

                // Convert to 64 again and return.
                return Convert.ToBase64String(state);
            }
        }

        public static bool VerifyPK3(string input, ushort sessionID, uint timeSecs, uint timeMillis, string encodedString)
        {
            var expectedEncodedString = EncodePK3(input, sessionID, timeSecs, timeMillis);
            return encodedString == expectedEncodedString;
        }
    }
}
