/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.IO;
using System;
using System.Collections.Generic;

namespace Imlight.Common.MessageLayer;

public static class MessageElementReader {
    private static readonly IReadOnlyDictionary<string, Func<BitReader, object>> s_dmlReaders
        = new Dictionary<string, Func<BitReader, object>>()
        {
            { "BYT",   (r)   => r.ReadInt8()                        },
            { "BOOL",  (r)   => r.ReadBool()                        },
            { "UBYT",  (r)   => r.ReadUInt8()                       },
            { "UBYTE", (r)   => r.ReadUInt8()                       },
            { "SHRT",  (r)   => r.ReadInt16()                       },
            { "USHRT", (r)   => r.ReadUInt16()                      },
            { "USHORT",(r)   => r.ReadUInt16()                      },
            { "INT",   (r)   => r.ReadInt32()                       },
            { "UINT",  (r)   => r.ReadUInt32()                      },
            { "STR",   (r)   => r.ReadString()                      },
            { "WSTR",  (r)   => new WideByteString(r.ReadWString()) },
            { "FLT",   (r)   => r.ReadFloat()                       },
            { "DBL",   (r)   => r.ReadDouble()                      },
            { "GID",   (r)   => r.ReadUInt64()                      },
        };

    /// <summary>
    /// Reads a DML (Distributed Message Layer) element from the given BitReader using the specified type.
    /// </summary>
    /// <param name="reader">The BitReader to read from.</param>
    /// <param name="type">The type of the DML object to read.</param>
    /// <returns>The DML object read from the BitReader.</returns>
    public static object ReadDml(BitReader reader, string type) {
        if (s_dmlReaders.TryGetValue(type, out var readerFunc)) {
            return readerFunc(reader);
        }

        throw new ArgumentException($"Unknown type: {type}");
    }
}
