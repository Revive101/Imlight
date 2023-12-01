/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.IO;
using System;
using System.Collections.Generic;

namespace Imlight.Common.MessageLayer;

public static class MessageElementWriters {
    private static readonly IReadOnlyDictionary<string, Action<BitWriter, object>> s_dmlWriters
        = new Dictionary<string, Action<BitWriter, object>>()
        {
            { "BYT",   (r, v)   => r.WriteInt8((sbyte)v)             },
            { "BOOL",  (r, v)   => r.WriteUInt8((byte)v)             },
            { "UBYT",  (r, v)   => r.WriteUInt8((byte)v)             },
            { "UBYTE", (r, v)   => r.WriteUInt8((byte)v)             },
            { "SHRT",  (r, v)   => r.WriteInt16((short)v)            },
            { "USHRT", (r, v)   => r.WriteUInt16((ushort)v)          },
            { "USHORT",(r, v)   => r.WriteUInt16((ushort)v)          },
            { "INT",   (r, v)   => r.WriteInt32((int)v)              },
            { "UINT",  (r, v)   => r.WriteUInt32((uint)v)            },
            { "STR",   (r, v)   => r.WriteString((ByteString)v)      },
            { "WSTR",  (r, v)   => r.WriteWString((WideByteString)v) },
            { "FLT",   (r, v)   => r.WriteFloat((float)v)            },
            { "DBL",   (r, v)   => r.WriteDouble((double)v)          },
            { "GID",   (r, v)   => r.WriteUInt64((ulong)v)           },
        };

    /// <summary>
    /// Writes a DML (Distributed Message Layer) element to the specified BitWriter.
    /// </summary>
    /// <param name="writer">The BitWriter to write to.</param>
    /// <param name="type">The type of the DML element.</param>
    /// <param name="value">The value of the DML element.</param>
    public static void WriteDml(BitWriter writer, string type, object value) {
        if (s_dmlWriters.TryGetValue(type, out var writerFunc)) {
            writerFunc(writer, value);
            return;
        }

        throw new ArgumentException($"Unknown type: {type}");
    }
}
