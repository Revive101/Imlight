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

namespace Imlight.CoreLib.Shared.Cryptography;

internal static class SessionKey {

    /// <summary>
    /// Generates a hash for the given input string and salt using SHA256 algorithm.
    /// </summary>
    /// <param name="input">The input string to generate hash for.</param>
    /// <param name="salt">The salt value to use for generating hash.</param>
    /// <returns>The generated hash as a base64 encoded string.</returns>
    internal static string GenerateHash(string input, ulong salt) {
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
    internal static bool ValidateHash(string input, ulong salt, string expectedHash) {
        var generatedHash = GenerateHash(input, salt);
        return generatedHash.Equals(expectedHash);
    }
    
}
