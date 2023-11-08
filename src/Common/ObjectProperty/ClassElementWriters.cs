using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using SharpDX;
using System;
using System.Collections.Generic;

namespace Imlight.Common.ObjectProperty;

public static class ClassElementWriters {
    private static readonly Dictionary<Type, Action<BitWriter, object>> s_primitiveWriters = new()
    {
        { typeof(byte),           (r, v) => r.WriteUInt8((byte)v)                },
        { typeof(char),           (r, v) => r.WriteUInt8((byte)v)                },
        { typeof(bool),           (r, v) => r.WriteBit((bool)v)                  },
        { typeof(short),          (r, v) => r.WriteInt16((short)v)               },
        { typeof(ushort),         (r, v) => r.WriteUInt16((ushort)v)             },
        { typeof(int),            (r, v) => r.WriteInt32((int)v)                 },
        { typeof(uint),           (r, v) => r.WriteUInt32((uint)v)               },
        { typeof(long),           (r, v) => r.WriteInt64((long)v)                },
        { typeof(ulong),          (r, v) => r.WriteUInt64((ulong)v)              },
        { typeof(ByteString),     (r, v) => r.WriteString((ByteString)v)         },
        { typeof(WideByteString), (r, v) => r.WriteWString((WideByteString)v)    },
        { typeof(float),          (r, v) => r.WriteFloat((float)v)               },
        { typeof(Vector3),        (r, v) => r.WriteVector3((Vector3)v)           },
        { typeof(Quaternion),     (r, v) => r.WriteQuaternion((Quaternion)v)     },
        { typeof(Matrix),         (r, v) => r.WriteMatrix((Matrix)v)             },
        { typeof(Color),          (r, v) => r.WriteColor((Color)v)               },
        { typeof(Color3),         (r, v) => r.WriteColor3((Color3)v)             },
        { typeof(Rectangle),      (r, v) => r.WriteRectangle((Rectangle)v)       },
        { typeof(RectangleF),     (r, v) => r.WriteRectangleF((RectangleF)v)     },
        { typeof(Vector2),        (r, v) => r.WriteVector2((Vector2)v)           },
        { typeof(Point),          (r, v) => r.WriteVector2((Point)v)             },
        { typeof(Bui2),           (r, v) => r.WriteBits((Bui2)v, 2)              },
        { typeof(Bui4),           (r, v) => r.WriteBits((Bui4)v, 4)              },
        { typeof(Bui5),           (r, v) => r.WriteBits((Bui5)v, 5)              },
        { typeof(Bui7),           (r, v) => r.WriteBits((Bui7)v, 7)              },
        { typeof(GID),            (r, v) => r.WriteUInt64((GID)v)                },
        { typeof(S24),            (r, v) => r.WriteBits((S24)v, 24)              },
        { typeof(U24),            (r, v) => r.WriteBits((U24)v, 24)              },
    };

    public static Action<BitWriter, object> TryGetWriter(Type r) {
        if (s_primitiveWriters.TryGetValue(r, out var func)) {
            return func!;
        }

        return null!;
    }
}
