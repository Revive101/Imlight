/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Text;
using System.Security.Cryptography;

namespace Imlight.CoreLib.Shared.Cryptography;

public static class PassKey3 {

    /// <summary>
    /// Encodes the input string using SHA512 algorithm and session information to generate a passkey.
    /// </summary>
    /// <param name="input">The input string to be encoded.</param>
    /// <param name="sessionID">The session ID.</param>
    /// <param name="timeSecs">The time in seconds.</param>
    /// <param name="timeMillis">The time in milliseconds.</param>
    /// <returns>The generated passkey.</returns>
    public static string EncodePK3(string input, ushort sessionID, uint timeSecs, uint timeMillis) {
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
    public static bool VerifyPK3(string input, ushort sessionID, uint timeSecs, uint timeMillis, string encodedString) {
        var expectedEncodedString = EncodePK3(input, sessionID, timeSecs, timeMillis);
        
        return encodedString == expectedEncodedString;
    }

}
