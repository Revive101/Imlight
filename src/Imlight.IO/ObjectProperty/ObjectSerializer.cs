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

namespace Imlight.IO.ObjectProperty
{
    public class ObjectSerializer
    {
        public enum Mode
        {
            Compact,
            Verbose
        }

        [Flags]
        public enum SerializerFlags
        {
            UseFlags      = 1 << 0, // States the serializer should use these flags for deserialization.
            CompactLength = 1 << 1, // Length prefixes are compacted into smaller data types whenever possible.
            StringEnums   = 1 << 2, // Some enums are made into strings.
            ZLibCompress  = 1 << 3, // Use ZLib compression.
            AlwaysEncode  = 1 << 4, // Always serialize properties with bitflag `8`.
        }

        [Flags]
        public enum PropertyFlags
        {
            Save = 1              << 0,
            Copy = 1              << 1,
            Public = 1            << 2,
            Transmit = 1          << 3,
            AuthorityTransmit = 1 << 4,
            Persistent = 1        << 5,
            Deprecated = 1        << 6,
            NoScript = 1          << 7,
            Encode = 1            << 8,
            Blob = 1              << 9,

            Immutable = 1         << 16,
            FileName = 1          << 17,
            Color = 1             << 18,

            Bits = 1              << 20,
            Enum = 1              << 21,
            Localized = 1         << 22,
            StringKey = 1         << 23,
            ObjectID = 1          << 24,
            ReferenceID = 1       << 25,
                                  
            ObjectName = 1        << 27,
            HasBaseClass = 1      << 28,

        }

        public Mode SerializerMode { get; init; }
        public SerializerFlags Options { get; private set; }
        public PropertyFlags PropertyMask { get; init; }
        private BitBuffer _currentReader;
        private byte[] _currentBuffer;

        private static readonly Dictionary<Type, Func<BitBuffer, object>> _primitiveReaders = new()
        {
            { typeof(byte),         (r) => r.ReadUInt8()       },
            { typeof(char),         (r) => r.ReadUInt8()       },
            { typeof(bool),         (r) => r.ReadBit()         },
            { typeof(short),        (r) => r.ReadUInt16()      },
            { typeof(ushort),       (r) => r.ReadInt16()       },
            { typeof(int),          (r) => r.ReadInt32()       },
            { typeof(uint),         (r) => r.ReadUInt32()      },
            { typeof(long),         (r) => r.ReadInt64()       },
            { typeof(ulong),        (r) => r.ReadUInt64()      },
            { typeof(string),       (r) => r.ReadString()      },
            { typeof(float),        (r) => r.ReadString()      },
            { typeof(Vector3),      (r) => r.ReadVector3()     },
            { typeof(Quaternion),   (r) => r.ReadQuaternion()  },
            { typeof(Matrix),       (r) => r.ReadMatrix()      },
            { typeof(Color),        (r) => r.ReadColor()       },
            { typeof(Rectangle),    (r) => r.ReadRectangle()   },
            { typeof(RectangleF),   (r) => r.ReadRectangleF()  },
            { typeof(Vector2),      (r) => r.ReadVector2()     },
            { typeof(Point),        (r) => r.ReadVector2()     },
            { typeof(TwoBitByte),   (r) => r.ReadBits<byte>(2) },
            { typeof(FourBitByte),  (r) => r.ReadBits<byte>(4) },
            { typeof(FiveBitByte),  (r) => r.ReadBits<byte>(5) },
            { typeof(SevenBitByte), (r) => r.ReadBits<byte>(7) },
            { typeof(GID),          (r) => r.ReadUInt64()      },
        };

        /// <summary>
        /// Creates a new ObjectSerializer.
        /// </summary>
        /// <param name="mode">Dictates the mode for the serializer; shallow or deep. Shallow will skip lengths and process properties in order of flags.</param>
        /// <param name="flags">The bit flags used by the serializer during process.</param>
        /// <param name="propertyFlags">The property mask, if shallow mode is set.</param>
        public ObjectSerializer(
            Mode mode = Mode.Compact, 
            SerializerFlags options = 0,
            PropertyFlags propertyFlagMask = PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit)
        {
            this.SerializerMode = mode;
            this.Options = options;
            this.PropertyMask = propertyFlagMask;
        }

        public PropertyClass Deserialize(byte[] buffer)
        {
            var reader = new BitBuffer(buffer);
            return Deserialize(reader);
        }

        public PropertyClass Deserialize(BitBuffer buffer)
        {
            SetSerializerData(buffer);

            var typeOID = buffer.ReadUInt32();
            if (typeOID == 0 || !TryPreloadObject(typeOID, out var propertyClass))
            {
                Log.Logger.Error($"Could not load object with hash: [{typeOID}]");
                return null;
            }

            var typeSize = (SerializerMode == Mode.Verbose) ? buffer.ReadUInt32() - 32 : 0;

            switch (SerializerMode)
            {
                case Mode.Compact:
                    return DeserializeCompact(buffer, propertyClass);
                default:
                    return DeserializeVerbose(buffer, propertyClass, typeSize);
            }
        }

        private PropertyClass DeserializeCompact(BitBuffer buffer, PropertyClass propertyClass)
        {
            // In shallow mode, the serializer will pass through properties in order under a certain bit mask.
            // Lengths and property hashes are not included.

            var fields = GetPropertyClassFields(propertyClass);

            var filteredFields = fields
                .Where(x => (x.GetCustomAttribute<PropertyAttribute>().Flags & (uint)PropertyMask) != 0);
            foreach (var field in filteredFields)
            {
                DeserializeObjectField(buffer, propertyClass, field);
            }

            return propertyClass;
        }

        private PropertyClass DeserializeVerbose(BitBuffer bufReader, PropertyClass propertyClass, uint size)
        {
            // In deep mode, the serializer allows for empty values as dictated by property hashes and sizes.

            var fields = GetPropertyClassFields(propertyClass);
            var fieldDictionary = fields.ToDictionary(x => x.GetCustomAttribute<PropertyAttribute>().Hash, x => x);

            var objectStart = bufReader.TellBitPos();
            while (bufReader.TellBitPos() - objectStart < size)
            {
                var propertyStart = bufReader.TellBitPos();
                var propertySize = bufReader.ReadUInt32();
                var propertyHash = bufReader.ReadUInt32();

                if (!fieldDictionary.TryGetValue(propertyHash, out var fieldRecord))
                {
                    Log.Logger.Error($"Could not find property with property hash [{propertyHash}] in PropertyClass [{propertyClass.GetType()}].");
                }

                DeserializeObjectField(bufReader, propertyClass, fieldRecord);

                bufReader.SeekBit((int)(propertyStart + propertySize));
            }

            return propertyClass;
        }

        private void SetSerializerData(BitBuffer reader)
        {
            var useFlags = (this.Options & SerializerFlags.UseFlags) == SerializerFlags.UseFlags;

            if (useFlags)
            {
                Options = (SerializerFlags)reader.ReadUInt32();
                var compress = (Options & SerializerFlags.ZLibCompress);

                switch (compress)
                {
                    case SerializerFlags.ZLibCompress:
                        this._currentReader = ZLibUtil.Decompress(reader);
                        this._currentBuffer = _currentReader.GetData();
                        break;
                    default:
                        this._currentReader = reader;
                        this._currentBuffer = reader.GetData();
                        break;
                }
            }
            else
            {
                this._currentReader = reader;
                this._currentBuffer = reader.GetData();
            }
        }

        private void DeserializeObjectField(BitBuffer reader, PropertyClass propertyClass,  FieldInfo field)
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
                    var val = DeserializeObjectValue(reader, field);
                    vec.Add(val);
                }

                SetValue(propertyClass, field, vec);
            }
            else
            {
                var val = DeserializeObjectValue(reader, field);
                SetValue(propertyClass, field, val);
            }
        }

        private object DeserializeObjectValue(BitBuffer reader, FieldInfo field)
        {
            if (!_primitiveReaders.TryGetValue(field.FieldType, out var readerFunc))
            {
                // If it's not a primitive data type, it's another PropertyClass.
                var subVal = Deserialize(reader);

                return subVal;
            }
            else
            {
                return readerFunc.Invoke(reader);
            }
        }

        private static bool TryPreloadObject(uint hash, out PropertyClass propClass)
        {
            propClass = TypeCache.Dispatch(hash);
            return propClass != null;
        }

        public static void SetValue(object inputObject, FieldInfo field, object fieldValue)
        {
            Type fieldType = field.FieldType;
            if (fieldValue == null) return;

            switch (fieldType.Name)
            {
                case "GID":
                    field.SetValue(inputObject, new GID((ulong)fieldValue));
                    break;
                case "TwoBitByte":
                    field.SetValue(inputObject, new TwoBitByte((byte)fieldValue));
                    break;
                case "FourBitByte":
                    field.SetValue(inputObject, new FourBitByte((byte)fieldValue));
                    break;
                case "FiveBitByte":
                    field.SetValue(inputObject, new FiveBitByte((byte)fieldValue));
                    break;
                case "SevenBitByte":
                    field.SetValue(inputObject, new SevenBitByte((byte)fieldValue));
                    break;
                case "LongWord":
                    field.SetValue(inputObject, new LongWord((byte)fieldValue));
                    break;
                case "ULongWord":
                    field.SetValue(inputObject, new ULongWord((byte)fieldValue));
                    break;
                default:
                    fieldValue = Convert.ChangeType(fieldValue, fieldType);
                    field.SetValue(inputObject, fieldValue);
                    break;
            }
        }

        private static IEnumerable<FieldInfo> GetPropertyClassFields(PropertyClass propClass)
        {
            if (propClass == null) throw new NullReferenceException(nameof(propClass));

            return propClass.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                .Where(x => Attribute.IsDefined(x, typeof(PropertyAttribute)))
                .OrderBy(x => x, new PropertyClassFieldComparer());
        }

        public class PropertyClassFieldComparer : IComparer<FieldInfo>
        {
            public int Compare(FieldInfo x, FieldInfo y)
            {
                if (x.DeclaringType != y.DeclaringType)
                {
                    return x.DeclaringType.IsAssignableFrom(y.DeclaringType) ? -1 : 1;
                }
                return x.MetadataToken.CompareTo(y.MetadataToken);
            }
        }
    }
}
