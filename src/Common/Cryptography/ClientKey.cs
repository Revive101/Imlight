/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Imlight.Common.IO;

namespace Imlight.Common.Cryptography;

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
    public static string HaskCK1(string input, ushort sessionID, uint timeSecs, uint timeMillis)
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
        // Do not do the first pass.
        var salt = $"{sessionID}{timeSecs}{timeMillis}";
        var secondPass = SecondaryEncrypt(input, salt);

        return encodedString == secondPass;
    }

    /// <summary>
    /// Constructs a new salted session key hash.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <param name="offerSeconds"></param>
    /// <param name="offerMilli"></param>
    /// <returns></returns>
    public static ByteString HashSessionKey(ushort sessionId, uint offerSeconds, uint offerMilli)
    {
        // Generate a cryptographically safe number.
        using var rng = new RNGCryptoServiceProvider();
        var randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        var randomNum = BitConverter.ToInt32(randomBytes, 0);
        randomNum = Math.Abs(randomNum);

        var combinedData = $"{randomNum}{sessionId}{offerSeconds}{offerMilli}";
        var dataBytes = Encoding.UTF8.GetBytes(combinedData);
        var hashBytes = SHA256.HashData(dataBytes);

        return Convert.ToBase64String(hashBytes);
    }

    private static string HashPassword(string password)
    {
        using var sha512 = SHA512.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        return Convert.ToBase64String(sha512.ComputeHash(passwordBytes));
    }

    public static string SecondaryEncrypt(string password, string seed)
    {
        using var sha512 = SHA512.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var seedBytes = Encoding.UTF8.GetBytes(seed);

        var hash = sha512.ComputeHash(passwordBytes.Concat(seedBytes).ToArray());

        return Convert.ToBase64String(hash);
    }
}