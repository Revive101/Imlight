using Imlight.Internals;
using Imlight.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using SharpDX;

namespace Imlight.IO
{
    public class ObjectSerializer
    {
        public enum Mode
        {
            Shallow,
            Deep
        }

        private static readonly Dictionary<Type, Func<BitBuffer, object>> _primitiveReaders = new()
        {
            { typeof(byte),       (r) => r.ReadUInt8()      },
            { typeof(char),       (r) => r.ReadUInt8()      },
            { typeof(bool),       (r) => r.ReadBit()        },
            { typeof(short),      (r) => r.ReadUInt16()      },
            { typeof(ushort),     (r) => r.ReadInt16()      },
            { typeof(int),        (r) => r.ReadInt32()      },
            { typeof(uint),       (r) => r.ReadUInt32()      },
            { typeof(long),       (r) => r.ReadInt64()      },
            { typeof(ulong),      (r) => r.ReadUInt64()      },
            { typeof(string),     (r) => r.ReadString()      },
            { typeof(float),      (r) => r.ReadString()      },
            { typeof(Vector3),    (r) => r.ReadVector3()    },
            { typeof(Quaternion), (r) => r.ReadQuaternion() },
            { typeof(Matrix),     (r) => r.ReadMatrix()     },
            { typeof(Color),      (r) => r.ReadColor()      },
            { typeof(Rectangle),  (r) => r.ReadRectangle()  },
            { typeof(RectangleF), (r) => r.ReadRectangleF() },
            { typeof(Vector2),    (r) => r.ReadVector2()    },
            { typeof(Point),      (r) => r.ReadVector2()    },
        };

        public static PropertyClass DeserializeObject(byte[] buffer, Mode mode = Mode.Shallow)
        {
            var reader = new BitBuffer(buffer);
            var typeHash = reader.ReadUInt32();

            if (typeHash == 0 || !TryPreloadObject(typeHash, out var propertyClass))
            {
                Log.Logger.Error($"Could not load object with hash: [{typeHash}]");
                return null;
            }

            var objectSize = buffer.Length;
            var v = reader.TellBitPos();
            var r = reader.TellBitPos() - 32;

            // Get the fields from our pre-defined PropertyClass
            var fields = GetFields(propertyClass);
            var fieldDictionary = fields.ToDictionary(x => x.GetCustomAttribute<PropertyAttribute>().Hash, x => x);

            while (reader.TellBitPos() < objectSize)
            {
                var fieldStart = reader.TellBitPos();
                var fieldHash = reader.ReadUInt32();

                if (!fieldDictionary.TryGetValue(fieldHash, out var fieldRecord))
                {
                    Log.Logger.Error($"Could not find property with property hash [{fieldHash}] in PropertyClass [{propertyClass.GetType()}].");
                }

                DeserializeObjectField(ref reader, ref fieldRecord);

                reader.SeekBit((int)(fieldStart + objectSize));
            }

            return propertyClass;
        }

        private static void DeserializeObjectField(ref BitBuffer reader,  ref FieldInfo field)
        {
            var fieldType = field.FieldType;
            if (fieldType.IsList())
            {
                var innerType = fieldType.GetGenericArguments()[0];
                var vecType = typeof(List<>).MakeGenericType(innerType);
                var vec = (IList)Activator.CreateInstance(vecType);

                var vecLength = reader.ReadUInt32();
                while (vec.Count < vecLength)
                {
                    var val = DeserializeObjectValue(ref reader, field);
                    vec.Add(val);
                }

                field.SetValue(field, vec);
            }
            else
            {
                var val = DeserializeObjectValue(ref reader, field);
                field.SetValue(field, val);
            }
        }

        private static object DeserializeObjectValue(ref BitBuffer reader, FieldInfo field)
        {
            if (!_primitiveReaders.TryGetValue(field.FieldType, out var readerFunc))
            {
                // If it's not a primitive data type, it's another PropertyClass.
                var propertySize = 0;
                var subBuffer = reader.ReadBytes(propertySize);
                var subVal = DeserializeObject(subBuffer);

                return subVal;
            }
            else
            {
                return readerFunc.Invoke(reader);
            }
        }

        private static bool TryPreloadObject(uint hash, out PropertyClass propClass)
        {
            propClass = Types.Dispatch(hash);
            return propClass != null;
        }

        private static IEnumerable<FieldInfo> GetFields(PropertyClass propClass)
        {
            if (propClass == null) throw new NullReferenceException(nameof(propClass));

            return propClass.GetType().GetFields()
                .Where(x => Attribute.IsDefined(x, typeof(PropertyAttribute)));
        }
    }
}
