/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
