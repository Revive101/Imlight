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
            { "gid",              typeof(ulong)         },
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

            // Unknown
            { "bui2",             typeof(byte)          },
            { "bui4",             typeof(byte)          },
            { "bui5",             typeof(byte)          },
            { "bui7",             typeof(byte)          },
            { "s24",              typeof(int)           }, // Has some relation to the CSR. 4 byte size so I'm reading it as int.
        };

        public static bool TryGetType(string internalTypeName, out Type type)
        {
            return _internalTypeTranslationDict.TryGetValue(internalTypeName, out type);
        }
    }
}
