/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Text;
using System.Security.Cryptography;

namespace Imlight.CoreLib.Shared.Cryptography;

internal static class PassKey3 {

    /// <summary>
    /// Encodes the input string using SHA512 algorithm and session information to generate a passkey.
    /// </summary>
    /// <param name="input">The input string to be encoded.</param>
    /// <param name="sessionID">The session ID.</param>
    /// <param name="timeSecs">The time in seconds.</param>
    /// <param name="timeMillis">The time in milliseconds.</param>
    /// <returns>The generated passkey.</returns>
    internal static string EncodePK3(string input, ushort sessionID, uint timeSecs, uint timeMillis) {
        using var sha512 = SHA512.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        sha512.TransformBlock(bytes, 0, bytes.Length, null, 0);

        var sessionInfoBytes = Encoding.UTF8.GetBytes($"{sessionID}{timeSecs}{timeMillis}");
        sha512.TransformFinalBlock(sessionInfoBytes, 0, sessionInfoBytes.Length);

        var passkey3Bytes = sha512.Hash!;
        var passkey3 = Convert.ToBase64String(passkey3Bytes);

        return passkey3;
    }

    /// <summary>
    /// Verifies the encoded string against the expected encoded string generated using the input, session ID, time in seconds and time in milliseconds.
    /// </summary>
    /// <param name="input">The input string to generate the expected encoded string.</param>
    /// <param name="sessionID">The session ID to generate the expected encoded string.</param>
    /// <param name="timeSecs">The time in seconds to generate the expected encoded string.</param>
    /// <param name="timeMillis">The time in milliseconds to generate the expected encoded string.</param>
    /// <param name="encodedString">The encoded string to verify.</param>
    /// <returns>True if the encoded string matches the expected encoded string, false otherwise.</returns>
    internal static bool VerifyPK3(string input, ushort sessionID, uint timeSecs, uint timeMillis, string encodedString) {
        var expectedEncodedString = EncodePK3(input, sessionID, timeSecs, timeMillis);
        
        return encodedString == expectedEncodedString;
    }

}
