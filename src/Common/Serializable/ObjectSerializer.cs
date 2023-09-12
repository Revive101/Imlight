/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Imlight.Common.IO;
using Imlight.Common.Serializable.Caches;
using Imlight.Common.Serializable.ObjectProperty;
using Imlight.Common.Utilities;
using SharpDX;

namespace Imlight.Common.Serializable;

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
        None,
        UseFlags       = 1 << 0, // States the serializer should use these flags for deserialization.
        CompactLength  = 1 << 1, // Length prefixes are compacted into smaller data types whenever possible.
        StringEnums    = 1 << 2, // Some enums are made into strings.
        ZLibCompress   = 1 << 3, // Use ZLib compression.
        AlwaysEncode   = 1 << 4, // Always serialize properties with bitflag `8`.
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
        ObjectId = 1          << 24,
        ReferenceId = 1       << 25,
                                  
        ObjectName = 1        << 27,
        HasBaseClass = 1      << 28,
    }

    protected Mode SerializerMode { get; set; }
    protected SerializerFlags Options { get; set; }
    protected PropertyFlags PropertyMask { get; set; }

    private static readonly Dictionary<Type, Func<BitIterator, object>> PrimitiveReaders = new()
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
        { typeof(TwoBitByte),     (r) => r.ReadBits<byte>(2)       },      
        { typeof(FourBitByte),    (r) => r.ReadBits<byte>(4)       },      
        { typeof(FiveBitByte),    (r) => r.ReadBits<byte>(5)       },      
        { typeof(SevenBitByte),   (r) => r.ReadBits<byte>(7)       },      
        { typeof(GID),            (r) => r.ReadUInt64()                    },
        { typeof(LongWord),       (r) => r.ReadBits<LongWord>(24)  },
        { typeof(ULongWord),      (r) => r.ReadBits<ULongWord>(24) },
    };
    private static readonly Dictionary<Type, Action<BitIterator, object>> PrimitiveWriters = new()
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
        { typeof(WideByteString), (r, v) => r.WriteWString((WideByteString)v) },
        { typeof(float),          (r, v) => r.WriteFloat((float)v)               },
        { typeof(Vector3),        (r, v) => r.WriteVector3((Vector3)v)           },
        { typeof(Quaternion),     (r, v) => r.WriteQuaternion((Quaternion)v)     },
        { typeof(Matrix),         (r, v) => r.WriteMatrix((Matrix)v)             },
        { typeof(Color),          (r, v) => r.WriteColor((Color)v)               },
        { typeof(Color3),         (r, v) => r.WriteColor3((Color3)v)             },
        { typeof(Rectangle),      (r, v) => r.WriteRectangle((Rectangle)v)       },
        { typeof(RectangleF),     (r, v) => r.WriteRectangleF((RectangleF)v)     },
        { typeof(Vector2),        (r, v) => r.WriteVector2((Vector2)v)           },
        { typeof(Point),          (r, v) => r.WriteVector2((Point)v)           },
        { typeof(TwoBitByte),     (r, v) => r.WriteBits((TwoBitByte)v, 2)   },
        { typeof(FourBitByte),    (r, v) => r.WriteBits((FourBitByte)v, 4)  },
        { typeof(FiveBitByte),    (r, v) => r.WriteBits((FiveBitByte)v, 5)  },
        { typeof(SevenBitByte),   (r, v) => r.WriteBits((SevenBitByte)v, 7) },
        { typeof(GID),            (r, v) => r.WriteUInt64((GID)v)              },
        { typeof(LongWord),       (r, v) => r.WriteBits((LongWord)v, 24)    },
        { typeof(ULongWord),       (r, v) => r.WriteBits((ULongWord)v, 24)  },
    };

    private const byte RecursionLimit = byte.MaxValue / 2;
    private byte _currentRecursionLevel;

    /// <summary>
    /// Creates a new ObjectSerializer.
    /// </summary>
    /// <param name="mode">Dictates the mode for the serializer; shallow or deep. Shallow will skip lengths and process properties in order of flags.</param>
    /// <param name="options"></param>
    /// <param name="propertyFlagMask"></param>
    public ObjectSerializer(
        Mode mode = Mode.Compact, 
        SerializerFlags options = 0,
        PropertyFlags propertyFlagMask = PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit)
    {
        this.SerializerMode = mode;
        this.Options = options;
        this.PropertyMask = propertyFlagMask;
    }
        
    public virtual ObjectSerializer WithMode(Mode mode)
    {
        this.SerializerMode = mode;
        return this;
    }

    public virtual ObjectSerializer WithSerializerFlags(SerializerFlags flags)
    {
        this.Options = flags;
        return this;
    }

    public virtual ObjectSerializer WithPropertyFlags(PropertyFlags flags)
    {
        this.PropertyMask = flags;
        return this;
    }

    #region Serialize
        
    public ByteString Serialize(PropertyClass propertyClass)
    {
        // Can't serialize the base PropertyClass if it isn't there, eh?
        if (propertyClass == null)
        {
            throw new ArgumentNullException(nameof(propertyClass));
        }

        var serializedData = SerializeInternal(propertyClass);
        var compress = (Options & SerializerFlags.UseFlags) != 0
                       && (Options & SerializerFlags.ZLibCompress) != 0;
        if (!compress)
            return serializedData;
            
        // Compress the serialized data.
        var uncompressedSize = serializedData.Length;
        var compressedData = Compress(serializedData);

        var bufferSize = compressedData.Length + 4;
        var tempBuffer = new byte[bufferSize];

        using var memoryStream = new MemoryStream(tempBuffer);
        using var binaryWriter = new BinaryWriter(memoryStream);

        binaryWriter.Write(uncompressedSize);
        binaryWriter.Write(compressedData);

        return tempBuffer;
    }

    private ByteString SerializeInternal(PropertyClass propertyClass)
    {
        var writer = new BitIterator();

        // Set compact length mode on BitIterator, if the serializer flags are set as so.
        var compactLength = (Options & SerializerFlags.UseFlags) != 0
                            && (Options & SerializerFlags.CompactLength) != 0;
        writer.CompactLength = compactLength;

        if (!PreWriteObject(writer, propertyClass))
        {
            return writer.GetData();
        }

        // Write the length bytes and set it later.
        var lengthLocation = writer.TellBitPos();
        if (SerializerMode == Mode.Verbose)
        {
            writer.WriteUInt32(0);
        }

        switch (SerializerMode)
        {
            case Mode.Compact:
                SerializeCompact(writer, propertyClass);
                break;
            case Mode.Verbose:
            default:
                SerializeVerbose(writer, propertyClass);
                break;
        }

        // If this is verbose mode, we need to go back to those length bytes and set them.
        if (SerializerMode != Mode.Verbose)
            return new ByteString(writer.GetData());
        writer.SeekBit(lengthLocation);
        writer.WriteUInt32((uint)(writer.GetData().Length - 2));

        return new ByteString(writer.GetData());
    }

    private void SerializeCompact(BitIterator writer, PropertyClass propertyClass)
    {
        // In compact mode, the serializer will pass through properties in order under a certain bit mask.
        // Lengths and property hashes are not included.

        // Get all the fields of the PropertyClass. Filter using out bit mask, and remove any property
        // that carries the Deprecated flag.
        var fields = GetPropertyClassFields(propertyClass);
        var filteredFields = fields
            .Where(x =>
            {
                var attr = x.GetCustomAttribute<PropertyAttribute>();
                if (attr is null) throw new Exception("This should never happen!");

                return ((PropertyFlags)attr.Flags & PropertyMask) == PropertyMask;
            });

        foreach (var field in filteredFields)
        {
            SerializeObjectField(writer, propertyClass, field);
        }
    }

    [Obsolete("WizUnraveler currently does not support serializing verbosely.")]
    private void SerializeVerbose(BitIterator writer, PropertyClass propertyClass)
    {
        // @TODO: Work on this. I can't find a portion in the client where serializing verbosely is necessary.
    }

    private void SerializeObjectField(BitIterator writer, PropertyClass propertyClass, FieldInfo field)
    {
        var fieldType = field.FieldType;

        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (ICollection)field.GetValue(propertyClass);

            var count = list?.Count ?? 0;
            WriteVectorCount(writer, count);

            // Can't serialize a list that isn't there.
            if (list is null) 
                return;

            foreach (var item in list)
            {
                SerializeObjectValue(writer, field, item);
            }
        }
        else
        {
            var fieldVal = field.GetValue(propertyClass);
            SerializeObjectValue(writer, field, fieldVal);
        }
    }

    private void SerializeObjectValue(BitIterator writer, FieldInfo field, object value)
    {
        var type = field.FieldType;
        var flags = (PropertyFlags)field.GetCustomAttribute<PropertyAttribute>()!.Flags;
        var writerFunc = PrimitiveWriters.TryGetValue(type, out var func) ? func : null;

        // Serialize enum if field type is enum or the ProperyFlags indicate bits or enum.
        if ((flags & (PropertyFlags.Bits | PropertyFlags.Enum)) != 0 || type.IsEnum)
        {
            if ((Options & SerializerFlags.UseFlags) != 0 && (Options & SerializerFlags.StringEnums) != 0)
            {
                writer.WriteString(value.ToString());
            }
            else
            {
                writer.WriteInt32((int)value);
            }
        }
        // Serialize as a primitive data type.
        else if (writerFunc != null)
        {
            writerFunc.Invoke(writer, value);
        }
        // If the value is null, we'll just serialize the property hash.
        else if (value is null)
        {
            PreWriteObject(writer, null);
        }
        // If we're here, it means that the value is another PropertyClass.
        else
        {
            var val = SerializeInternal((PropertyClass)value);
            writer.WriteBytes(val);
        }
    }

    private void WriteVectorCount(BitIterator buffer, int count)
    {
        if ((Options & SerializerFlags.UseFlags) != 0
            && (Options & SerializerFlags.CompactLength) != 0)
        {
            if (count >= 128)
            {
                buffer.WriteBit(1);
                buffer.WriteBits(count, 31);
            }
            else
            {
                buffer.WriteBit(0);
                buffer.WriteBits((byte)count, 7);
            }
        }
        else
        {
            buffer.WriteInt32(count);
        }
    }
        
    protected virtual bool PreWriteObject(BitIterator writer, PropertyClass propClass)
    {
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        if (propClass is null)
        {
            writer.WriteUInt32(0);
            return false;
        }

        if (propClass.GetHash() == 0)
        {
            Log.Logger.Error("PropertyClass hash set to 0 for serialization! Is it properly loaded?");

            return false;
        }

        writer.WriteUInt32(propClass.GetHash());
        return true;
    }

    #endregion

    #region Deserialize

    public PropertyClass Deserialize(byte[] buffer)
    {
        var reader = new BitIterator(buffer);
        return Deserialize(reader);
    }

    protected PropertyClass Deserialize(BitIterator buffer)
    {
        _currentRecursionLevel = 0;
            
        var compactLength = (Options & SerializerFlags.UseFlags) != 0
                            && (Options & SerializerFlags.CompactLength) != 0;
        buffer.CompactLength = compactLength;
            
        var compress = (Options & SerializerFlags.UseFlags) != 0
                       && (Options & SerializerFlags.ZLibCompress) != 0;

        if (!compress)
            return DeserializeInternal(buffer);
            
        // Decompress the data.
        var decompressedBuffer = new BitIterator(buffer.GetData());
        var uncompressedLength = buffer.ReadInt32();

        var compressedDataMinusHeader = new byte[buffer.GetData().Length - 4];
        Buffer.BlockCopy(buffer.GetData(), 
            4, 
            compressedDataMinusHeader, 
            0, 
            buffer.GetData().Length - 4);

        var decompressedData = Decompress(compressedDataMinusHeader);
        var deserializedData = DeserializeInternal(decompressedBuffer);
            
        return deserializedData;
    }

    private PropertyClass DeserializeInternal(BitIterator buffer)
    {
        //CheckRecursionDepth();
        //_currentRecursionLevel++;
            
        if (!PreloadObject(buffer, out var propertyClass))
        {
            return null;
        }

        var typeSize = (SerializerMode == Mode.Verbose) ? buffer.ReadUInt32() - 32 : 0;

        return SerializerMode switch
        {
            Mode.Compact => DeserializeCompact(buffer, propertyClass),
            _ => DeserializeVerbose(buffer, propertyClass, typeSize)
        };
    }

    private PropertyClass DeserializeCompact(BitIterator buffer, PropertyClass propertyClass)
    {
        // In compact mode, the serializer will pass through properties in order under a certain bit mask.
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

    private PropertyClass DeserializeVerbose(BitIterator bufReader, PropertyClass propertyClass, uint size)
    {
        // In verbose mode, the serializer will read property size and hashes.
        // Get all fields that intercept the property mask.
        var fieldDictionary = GetPropertyClassFields(propertyClass)
            .Where(x => ((PropertyFlags)x.GetCustomAttribute<PropertyAttribute>()!.Flags & PropertyMask) == PropertyMask)
            .ToDictionary(x => x.GetCustomAttribute<PropertyAttribute>()!.Hash, x => x);
            
        var objectStart = bufReader.TellBitPos();
        while (bufReader.TellBitPos() - objectStart < size)
        {
            var propertyStart = bufReader.TellBitPos();
            var propertySize = bufReader.ReadUInt32();
            var propertyHash = bufReader.ReadUInt32();

            if (fieldDictionary.TryGetValue(propertyHash, out var fieldRecord))
            {
                DeserializeObjectField(bufReader, propertyClass, fieldRecord);
            }
            else
            {
                //Log.Logger.Error($"Could not find property with property hash [{propertyHash}] " +
                //$"in PropertyClass [{propertyClass.GetType()}].");
            }

            bufReader.SeekBit((int)(propertyStart + propertySize));
        }
 
        // Seek to the end of the object.
        bufReader.SeekBit((int)(objectStart + size));
        return propertyClass;
    }

    private void DeserializeObjectField(BitIterator reader, PropertyClass propertyClass, FieldInfo field)
    {
        if (field is null) return;

        // Get the type of the field. If it's a list, we'll deserialize each object individually.
        // otherwise, we'll just deserialize the object as normal.
        var fieldType = field.FieldType;
        if (fieldType.IsGenericType 
            && fieldType.GetGenericTypeDefinition() == typeof(List<>))
        {
            // Create a generic list and iterate through the elements.
            var innerType = fieldType.GetGenericArguments()[0];
            var vecType = typeof(List<>).MakeGenericType(innerType);
            var vec = (IList)Activator.CreateInstance(vecType);

            var vecLength = ReadVectorCount(reader);
            for (int i = 0; i < vecLength; i++)
            {
                var val = DeserializeObjectValue(reader, innerType);
                if (val is null)
                {
                    vec?.Add(null);
                    continue;
                }
                    
                // For some reason, GID fails to be added to the generic list type.
                // @todo: fixme
                vec?.Add(innerType == typeof(GID) ? new GID((ulong)val) : val);
            }

            SetValue(propertyClass, field, vec);
        }
        else
        {
            var val = DeserializeObjectValue(reader, field.FieldType);
            SetValue(propertyClass, field, val);
        }
    }

    private object DeserializeObjectValue(BitIterator reader, Type type)
    {
        if (type.IsEnum)
        {
            // Read human-readable string if serializer bitflag is set.
            var stringEnum = (Options & SerializerFlags.UseFlags) != 0
                             && (Options & SerializerFlags.StringEnums) != 0;
            if (!stringEnum) 
                return reader.ReadUInt32();
                
            var str = reader.ReadString();
            if (Enum.TryParse(type, str, out var obj)) 
                return obj;
                    
            Log.Logger.Error($"[{nameof(ObjectSerializer)}]: Could not string string enum " +
                             $"when string enum bitflag was set.");
            return 0;

        }
        if (PrimitiveReaders.TryGetValue(type, out var readerFunc))
        {
            return readerFunc.Invoke(reader);
        }

        // If it's not a primitive data type, it's another PropertyClass.
        var subVal = DeserializeInternal(reader);
            
        // This is a failsafe. If we could not successfully deserialize the PropertyClass, the buffer alignment
        // will be just before the PropertyClass size. Read that size, and skip by that amount of bytes.
        if (subVal is null && SerializerMode == Mode.Verbose)
        {
            // Just return null if the buffer doesn't have enough bits to read.
            if (reader.TellBitPos() + 32 > reader.GetData().Length * 8) return null;

            var skip = reader.ReadInt32();
            reader.SeekBit(reader.TellBitPos() + skip - 32);
        }

        return subVal;
    }

    private void SetValue(object propertyClass, FieldInfo field, object fieldValue)
    {
        var fieldType = field.FieldType;
        if (fieldValue == null) return;

        if (fieldType.IsEnum)
        {
            var stringEnum = (Options & SerializerFlags.UseFlags) != 0
                             && (Options & SerializerFlags.StringEnums) != 0;
            field.SetValue(propertyClass, stringEnum 
                ? fieldValue 
                : Enum.ToObject(fieldType, (uint)fieldValue));
            return;
        }
            
        if (typeof(PropertyClass).IsAssignableFrom(fieldType))
        {
            field.SetValue(propertyClass, fieldValue);
            return;
        }

        switch (fieldType.Name)
        {
            case "GID":
                field.SetValue(propertyClass, new GID((ulong)fieldValue));
                break;
            case "TwoBitByte":
                field.SetValue(propertyClass, new TwoBitByte((byte)fieldValue));
                break;
            case "FourBitByte":
                field.SetValue(propertyClass, new FourBitByte((byte)fieldValue));
                break;
            case "FiveBitByte":
                field.SetValue(propertyClass, new FiveBitByte((byte)fieldValue));
                break;
            case "SevenBitByte":
                field.SetValue(propertyClass, new SevenBitByte((byte)fieldValue));
                break;
            case "LongWord":
                field.SetValue(propertyClass, new LongWord((byte)fieldValue));
                break;
            case "ULongWord":
                field.SetValue(propertyClass, new ULongWord((byte)fieldValue));
                break;
            case "Point":
                var pointCast = (Vector2)(fieldValue);
                field.SetValue(propertyClass, new Point((int)pointCast.X, (int)pointCast.Y));
                break;
            case "ByteString":
                field.SetValue(propertyClass, new ByteString(fieldValue.ToString()));
                break;
            case "WideByteString":
                field.SetValue(propertyClass, new WideByteString(fieldValue.ToString()));
                break;
            default:
                try
                {
                    var convertedVal = Convert.ChangeType(fieldValue, fieldType);
                    field.SetValue(propertyClass, convertedVal);
                }
                catch (Exception)
                {
                    Log.Logger.Error($"Could not convert field {fieldType.Name} " +
                                     $"In PropertyClass {propertyClass.GetType()}.");
                }
                break;
        }
    }
        
    private uint ReadVectorCount(BitIterator buffer)
    {
        if ((Options & SerializerFlags.UseFlags) != 0
            && (Options & SerializerFlags.CompactLength) != 0)
        {
            // If the LSB is 1, we're still using the regular length.
            return buffer.ReadBits<uint>(buffer.ReadBit() ? 31 : 7);
        }
        else
        {
            return buffer.ReadUInt32();
        }
    }
        
    protected virtual bool PreloadObject(BitIterator buffer, out PropertyClass propClass)
    {
        // Dispatch the hash to our type cache to see if we carry this type.
        var hash = buffer.ReadUInt32();
        propClass = TypeCache.Dispatch(hash);
        if (propClass is not null) 
            return true;
            
        // If we couldn't dispatch the hash in our normal type cache, check the server types cache.
        propClass = Secrets.ServerTypeCache.Dispatch(hash);
        return propClass is not null;
    }

    #endregion
        
    private static byte[] Compress(byte[] _bytes)
    {
        Deflater deflater = new(Deflater.BEST_COMPRESSION, false);
        deflater.SetInput(_bytes);
        deflater.Finish();

        using MemoryStream ms = new();
        byte[] outputBuffer = new byte[65536 * 4];
        while (deflater.IsNeedingInput == false)
        {
            ms.Write(outputBuffer, 0, deflater.Deflate(outputBuffer));

            if (deflater.IsFinished == true)
                break;
        }

        deflater.Reset();

        return ms.ToArray();
    }

    private static IEnumerable<byte> Decompress(byte[] bytes)
    {
        MemoryStream resStream = new();
        using (MemoryStream memoryStream = new(bytes))
        {
            using InflaterInputStream inflater = new(memoryStream);
            inflater.CopyTo(resStream);
        }

        return resStream.ToArray();
    }

    private static IEnumerable<FieldInfo> GetPropertyClassFields(PropertyClass propClass)
    {
        if (propClass == null) throw new NullReferenceException(nameof(propClass));

        return propClass.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
            .Where(x => Attribute.IsDefined(x, typeof(PropertyAttribute)))
            .OrderBy(x => x, new PropertyClassFieldComparer());
    }

    private class PropertyClassFieldComparer : IComparer<FieldInfo>
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

    private void CheckRecursionDepth()
    {
        if (_currentRecursionLevel >= RecursionLimit)
            throw new Exception($"ObjectSerialize met recursion depth limit!");
    }
}