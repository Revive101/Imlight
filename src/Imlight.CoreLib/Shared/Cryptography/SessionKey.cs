/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography;
using System.Text;

namespace Imlight.CoreLib.Shared.Cryptography;

public static class SessionKey {

    /// <summary>
    /// Generates a hash for the given input string and salt using SHA256 algorithm.
    /// </summary>
    /// <param name="input">The input string to generate hash for.</param>
    /// <param name="salt">The salt value to use for generating hash.</param>
    /// <returns>The generated hash as a base64 encoded string.</returns>
    public static string GenerateHash(string input, ulong salt) {
        var saltBytes = BitConverter.GetBytes(salt);
        var inputBytes = Encoding.UTF8.GetBytes(input);

        var combinedBytes = new byte[saltBytes.Length + inputBytes.Length];
        Buffer.BlockCopy(saltBytes, 0, combinedBytes, 0, saltBytes.Length);
        Buffer.BlockCopy(inputBytes, 0, combinedBytes, saltBytes.Length, inputBytes.Length);

        using (var sha256 = SHA256.Create()) {
            var hashBytes = sha256.ComputeHash(combinedBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }

    /// <summary>
    /// Validates the hash of the input string using the provided salt and expected hash.
    /// </summary>
    /// <param name="input">The input string to validate.</param>
    /// <param name="salt">The salt used to generate the hash.</param>
    /// <param name="expectedHash">The expected hash to compare against the generated hash.</param>
    /// <returns>True if the generated hash matches the expected hash, false otherwise.</returns>
    public static bool ValidateHash(string input, ulong salt, string expectedHash) {
        var generatedHash = GenerateHash(input, salt);
        return generatedHash.Equals(expectedHash);
    }
    
}
