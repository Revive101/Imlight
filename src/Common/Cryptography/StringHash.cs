/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;

namespace Imlight.Common.Cryptography;

public static class StringHash {
    /// <summary>
    /// Computes a hash value for the input string.
    /// </summary>
    /// <param name="input">The input string to compute the hash value for.</param>
    /// <returns>The computed hash value.</returns>
    public static uint Compute(string input) {
        int result = 0;

        var shift1 = 0;
        var shift2 = 32;
        foreach (char c in input) {
            var cb = (byte) c;

            result ^= (cb - 32) << shift1;

            if (shift1 > 24) {
                result ^= (cb - 32) >> shift2;
                if (shift1 >= 27) {
                    shift1 -= 32;
                    shift2 += 32;
                }
            }
            shift1 += 5;
            shift2 -= 5;
        }

        if (result < 0) {
            result = -result;
        }

        return (uint) result;
    }

    /// <summary>
    /// Computes a hash value for a property name and its type.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="type">The type of the property.</param>
    /// <returns>The computed hash value.</returns>
    public static uint HashPropertyName(string name, string type) {
        var typeHash = Compute(type);
        var propHash = Djb2(name) & 0x7FFF_FFFF;

        // Dropping the most-significant byte.
        return (typeHash + propHash) & 0xFFFF_FFFF;
    }

    private static uint Djb2(string str) {
        return str.Aggregate<char, uint>(5381, (current, c) => ((current << 5) + current) + c);
    }
}
