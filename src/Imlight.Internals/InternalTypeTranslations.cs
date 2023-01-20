using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imlight.Internals
{
    public static class InternalTypeTranslations
    {
        private static readonly IReadOnlyDictionary<string, Type> _internalTypeTranslationDict = new Dictionary<string, Type>()
        {
            // Primitive
            { "int",              typeof(int)           },
            { "unsigned int",     typeof(uint)          },
            { "short",            typeof(short)         },
            { "unsigned short",   typeof(ushort)        },
            { "std::string",      typeof(string)        },
            { "std::wstring",     typeof(string)        },
            { "long",             typeof(long)          },
            { "unsigned long",    typeof(ulong)         },
            { "float",            typeof(float)         },
            { "bool",             typeof(bool)          },
            { "double",           typeof(double)        },
            { "char",             typeof(char)          },
            { "wchar_t",          typeof(char)          },
            { "unsigned char",    typeof(byte)          },
            { "unsigned __int64", typeof(ulong)         },

            // Internal
            { "gid",              typeof(GID)           },
            { "Vector3D",         typeof(Vector3)       },
            { "Euler",            typeof(Vector3)       },
            { "Quaternion",       typeof(Quaternion)    },
            { "Matrix3x3",        typeof(Matrix)        },
            { "Color",            typeof(Color3)        },
            { "Rect<float>",      typeof(RectangleF)    },
            { "Rect<int>",        typeof(Rectangle)     },
            { "Point<float>",     typeof(Vector2)       },
            { "Point<int>",       typeof(Point)         },
            { "Size<int>",        typeof(Point)         },
            { "SerializedBuffer", typeof(string)        },
            { "SimpleVert",       typeof(string)        }, //@TODO: Find out what this is internally
            { "SimpleFace",       typeof(string)        }, //@TODO: Find out what this is internally

            { "bui2",             typeof(TwoBitByte)    }, // 2-bit byte
            { "bui4",             typeof(FourBitByte)   }, // 4-bit byte
            { "bui5",             typeof(FiveBitByte)   }, // 5-bit byte
            { "bui7",             typeof(SevenBitByte)  }, // 7-bit byte
            { "s24",              typeof(LongWord)      }, // 24-bit signed integer
            { "u24",              typeof(ULongWord)     }, // 24-bit signed integer
        };

        public static bool TryGetType(string internalTypeName, out Type type)
        {
            return _internalTypeTranslationDict.TryGetValue(internalTypeName, out type);
        }
    }
}
