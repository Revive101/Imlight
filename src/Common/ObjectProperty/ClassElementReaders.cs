using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using SharpDX;
using System;
using System.Collections.Generic;

namespace Imlight.Common.ObjectProperty;

public static class ClassElementReaders {
    private static readonly Dictionary<Type, Func<BitReader, object>> s_primitiveReaders = new()
    {
        { typeof(byte),           (r) => r.ReadUInt8()                     },
        { typeof(char),           (r) => r.ReadUInt8()                     },
        { typeof(bool),           (r) => r.ReadBit()                       },
        { typeof(short),          (r) => r.ReadInt16()                     },
        { typeof(ushort),         (r) => r.ReadUInt16()                    },
        { typeof(int),            (r) => r.ReadInt32()                     },
        { typeof(uint),           (r) => r.ReadUInt32()                    },
        { typeof(long),           (r) => r.ReadInt64()                     },
        { typeof(ulong),          (r) => r.ReadUInt64()                    },
        { typeof(ByteString),     (r) => r.ReadString()                    },
        { typeof(WideByteString), (r) => r.ReadWString()                   },
        { typeof(float),          (r) => r.ReadFloat()                     },
        { typeof(Vector3),        (r) => r.ReadVector3()                   },
        { typeof(Quaternion),     (r) => r.ReadQuaternion()                },
        { typeof(Matrix),         (r) => r.ReadMatrix()                    },
        { typeof(Color),          (r) => r.ReadColor()                     },
        { typeof(Color3),         (r) => r.ReadColor3()                    },
        { typeof(Rectangle),      (r) => r.ReadRectangle()                 },
        { typeof(RectangleF),     (r) => r.ReadRectangleF()                },
        { typeof(Vector2),        (r) => r.ReadVector2()                   },
        { typeof(Point),          (r) => r.ReadVector2()                   },
        { typeof(Bui2),           (r) => r.ReadBits<byte>(2)               },
        { typeof(Bui4),           (r) => r.ReadBits<byte>(4)               },
        { typeof(Bui5),           (r) => r.ReadBits<byte>(5)               },
        { typeof(Bui7),           (r) => r.ReadBits<byte>(7)               },
        { typeof(GID),            (r) => r.ReadUInt64()                    },
        { typeof(S24),            (r) => r.ReadBits<S24>(24)               },
        { typeof(U24),            (r) => r.ReadBits<U24>(24)               },
    };

    /// <summary>
    /// Tries to get a reader function for the specified type.
    /// </summary>
    /// <param name="r">The type to get the reader function for.</param>
    /// <returns>A reader function for the specified type, or null if one could not be found.</returns>
    internal static Func<BitReader, object> TryGetReader(Type r) {
        if (s_primitiveReaders.TryGetValue(r, out var func)) {
            return func!;
        }

        return null!;
    }
}
