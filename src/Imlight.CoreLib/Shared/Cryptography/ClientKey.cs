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
using System.Security.Cryptography;
using System.Text;
using Imcodec.IO;

namespace Imlight.CoreLib.Shared.Cryptography;

internal static class ClientKey {

    /// <summary>
    /// Constructs a new salted ClientKey1 hash.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <param name="sessionID">The ID of the given session.</param>
    /// <param name="timeSecs">The seconds the session started at, since epoch.</param>
    /// <param name="timeMillis">The milliseconds into the current second.</param>
    /// <returns>The salted hash.</returns>
    internal static string HaskCK1(string input, ushort sessionID, uint timeSecs, uint timeMillis) {
        var passwordHash = HashPassword(input);

        var salt = $"{sessionID}{timeSecs}{timeMillis}";
        
        return SecondaryEncrypt(passwordHash, salt);
    }

    /// <summary>
    /// Verify a ClientKey1 hash against an input.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <param name="sessionID">The ID of the given session.</param>
    /// <param name="timeSecs">The seconds the session started at, since epoch.</param>
    /// <param name="timeMillis">The milliseconds into the current second.</param>
    /// <param name="encodedString">The encoded string to compare against.</param>
    /// <returns>True if the hash matches, false otherwise.</returns>
    internal static bool VerifyCK1(string input, ushort sessionID, uint timeSecs, uint timeMillis, string encodedString) {
        // Do not do the first pass.
        var salt = $"{sessionID}{timeSecs}{timeMillis}";
        var secondPass = SecondaryEncrypt(input, salt);

        return encodedString == secondPass;
    }

    /// <summary>
    /// Constructs a new salted session key hash.
    /// </summary>
    /// <param name="sessionId">The ID of the given session.</param>
    /// <param name="offerSeconds">The seconds the session started at, since epoch.</param>
    /// <param name="offerMilli">The milliseconds into the current second.</param>
    /// <returns>The salted hash.</returns>
    internal static ByteString HashSessionKey(ushort sessionId, uint offerSeconds, uint offerMilli) {
        // Generate a cryptographically safe number.
        using var rng = RandomNumberGenerator.Create();
        var randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        var randomNum = BitConverter.ToInt32(randomBytes, 0);
        randomNum = Math.Abs(randomNum);

        var combinedData = $"{randomNum}{sessionId}{offerSeconds}{offerMilli}";
        var dataBytes = Encoding.UTF8.GetBytes(combinedData);
        var hashBytes = SHA256.HashData(dataBytes);

        return Convert.ToBase64String(hashBytes);
    }

    private static string HashPassword(string password) {
        using var sha512 = SHA512.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        return Convert.ToBase64String(sha512.ComputeHash(passwordBytes));
    }

    private static string SecondaryEncrypt(string password, string seed) {
        using var sha512 = SHA512.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var seedBytes = Encoding.UTF8.GetBytes(seed);

        var hash = sha512.ComputeHash([.. passwordBytes, .. seedBytes]);

        return Convert.ToBase64String(hash);
    }

}
