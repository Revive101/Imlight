/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
