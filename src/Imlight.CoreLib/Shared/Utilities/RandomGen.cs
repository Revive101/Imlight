/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Security.Cryptography;
using System.Text;
using Imcodec.Types;

namespace Imlight.CoreLib.Shared.Utilities;

public static class RandomGen {
    
    /// <summary>
    /// Represents a globally unique identifier (GUID) for the application.
    /// </summary>
    public static GID GenerateGUID() {
        var buffer = Guid.NewGuid().ToByteArray(); // generate a new GUID
        var ulongType = BitConverter.ToUInt64(buffer, 0);

        return new GID(ulongType);
    }

    /// <summary>
    /// Generates a hash value for the given input string using SHA256 algorithm.
    /// </summary>
    /// <param name="input">The input string to generate hash for.</param>
    /// <returns>The generated hash value as an unsigned long integer.</returns>
    public static ulong GenerateHash(string input) {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        var hash = BitConverter.ToUInt64(hashBytes, 0);
        
        return hash;
    }

}
