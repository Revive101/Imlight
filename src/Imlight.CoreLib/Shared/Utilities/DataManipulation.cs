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
using System.Linq;

namespace Imlight.CoreLib.Shared.Utilities;

public static class DataManipulation {

    /// <summary>
    /// Converts a hexadecimal string to a byte array.
    /// </summary>
    /// <param name="hex">The hexadecimal string to convert.</param>
    /// <returns>The resulting byte array.</returns>
    public static byte[] StringToByteArray(string hex) 
        => [.. Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))];

    /// <summary>
    /// Converts a spaced hexadecimal string to a byte array.
    /// </summary>
    /// <param name="str">The spaced hexadecimal string to convert.</param>
    /// <returns>A byte array representing the hexadecimal string.</returns>
    public static byte[] SpacedHexStringToBytes(string str) {
        str = str.Replace(" ", "");
        if (str.Length % 2 != 0) {
            throw new Exception("Hex string must have even number of characters");
        }

        // Convert each pair of characters to a byte and add to the output array
        var ret = new byte[str.Length / 2];
        for (var i = 0; i < str.Length; i += 2) {
            var byteString = str.Substring(i, 2);
            var b = Convert.ToByte(byteString, 16);
            ret[i / 2] = b;
        }

        return ret;
    }

}
