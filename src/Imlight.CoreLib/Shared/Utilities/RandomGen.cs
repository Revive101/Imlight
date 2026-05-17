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
