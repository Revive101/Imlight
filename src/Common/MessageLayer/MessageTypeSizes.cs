using System;
using System.Collections.Generic;

namespace Imlight.Common.MessageLayer;

public static class MessageTypeSizes {
    private static readonly IReadOnlyDictionary<string, byte> Sizes = new Dictionary<string, byte>() {
        { "BYT", 1 },
        { "BOOL", 1 },
        { "UBYT", 1 },
        { "UBYTE", 1 },
        { "SHRT", 2 },
        { "USHRT", 2 },
        { "USHORT", 2 },
        { "INT", 4 },
        { "UINT", 4 },
        { "STR", 2 },
        { "WSTR", 2 },
        { "FLT", 4 },
        { "DBL", 8 },
        { "GID", 8 }
    };

    /// <summary>
    /// Gets the size of the specified message type.
    /// </summary>
    /// <param name="type">The message type.</param>
    /// <returns>The size of the message type.</returns>
    public static byte GetSize(string type) {
        if (Sizes.TryGetValue(type, out var size)) {
            return size;
        }

        throw new ArgumentException($"Unknown type: {type}");
    }
}
