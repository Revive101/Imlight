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
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.Common.Caches;
using SharpDX;
using Serilog;
using static Imlight.Common.ObjectProperty.SerializerOptions;

namespace Imlight.Common.ObjectProperty;

public class ObjectSerializer {
    private const byte RecursionLimit = byte.MaxValue / 2;
    protected SerializerOptions Options;
    private int _recursionLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectSerializer"/> class with the specified options.
    /// </summary>
    /// <param name="options">The serializer options.</param>
    public ObjectSerializer() {
        this.Options = new SerializerOptions()
            .OnMode(Mode.Compact)
            .OnBehaviors(Behaviors.None)
            .OnPropertyMask(PropertyFlags.Transmit | PropertyFlags.AuthorityTransmit);
    }

    /// <summary>
    /// Returns a new instance of the ObjectSerializer with the specified SerializerOptions.Mode.
    /// </summary>
    /// <param name="mode">The SerializerOptions.Mode to set.</param>
    /// <returns>A new instance of the ObjectSerializer with the specified SerializerOptions.Mode.</returns>
    public virtual ObjectSerializer OnMode(Mode mode) {
        this.Options.SerializerMode = mode;
        return this;
    }

    /// <summary>
    /// Returns a new instance of the ObjectSerializer with the specified SerializerOptions.Behaviors flags.
    /// </summary>readbits
    /// <param name="flags">The SerializerOptions.Behaviors flags to set.</param>
    /// <returns>A new instance of the ObjectSerializer with the specified SerializerOptions.Behaviors flags.</returns>
    public virtual ObjectSerializer OnBehaviors(Behaviors flags) {
        this.Options.BehaviorFlags = flags;
        return this;
    }

    /// <summary>
    /// Returns a new instance of the ObjectSerializer with the specified property flags.
    /// </summary>
    /// <param name="flags">The property flags to set.</param>
    /// <returns>A new instance of the ObjectSerializer with the specified property flags.</returns>
    public virtual ObjectSerializer OnPropertyMask(PropertyFlags flags) {
        this.Options.PropertyMask = flags;
        return this;
    }

    #region Serialize

    /// <summary>
    /// Serializes a <see cref="PropertyClass"/> object into a <see cref="ByteString"/> using the specified <see cref="SerializerOptions"/>.
    /// </summary>
    /// <param name="propertyClass">The <see cref="PropertyClass"/> object to serialize.</param>
    /// <returns>A <see cref="ByteString"/> containing the serialized data.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="propertyClass"/> is null.</exception>
    public ByteString Serialize(PropertyClass propertyClass) {
        // Can't serialize the base PropertyClass if it isn't there, eh?
        if (propertyClass == null) {
            throw new ArgumentNullException(nameof(propertyClass));
        }

        // Configure to begin serialization.
        _recursionLevel = 0;
        var writer = new BitWriter();
        if (Options.BehaviorFlags.HasFlag(Behaviors.CompactLength)) {
            writer.WithCompactLengths();
        }

        // Serialize the first PropertyClass as-is.
        var serializedData = SerializeInternal(writer, propertyClass).GetData();

        // If the serializer flags are set to compress, we'll compress the serialized data.
        if (!Options.BehaviorFlags.HasFlag(Behaviors.Compress)) {
            // If not, we'll just return the serialized data.
            return serializedData;
        }

        writer = Compress(writer);
        return writer.GetData();
    }

    protected virtual bool PreWriteObject(BitWriter writer, PropertyClass propClass) {
        if (writer is null) {
            throw new ArgumentNullException(nameof(writer));
        }

        if (propClass is null) {
            writer.WriteUInt32(0);
            return false;
        }

        if (propClass.GetHash() == 0) {
            Logger.Error("PropertyClass hash set to 0 for serialization! Is it properly loaded?");

            return false;
        }

        writer.WriteUInt32(propClass.GetHash());
        return true;
    }

    protected virtual BitWriter Compress(BitWriter writer) {
        var uncompressedSize = writer.GetData().Length;
        var compressedData = Compression.Compress(writer.GetData());

        var bufferSize = compressedData.Length + 4;
        var tempBuffer = new byte[bufferSize];

        using var memoryStream = new MemoryStream(tempBuffer);
        using var binaryWriter = new BinaryWriter(memoryStream);

        binaryWriter.Write(uncompressedSize);
        binaryWriter.Write(compressedData);

        return new BitWriter(tempBuffer);
    }

    private BitWriter SerializeInternal(BitWriter writer, PropertyClass propertyClass) {
        if (_recursionLevel >= RecursionLimit) {
            throw new Exception("Recursion limit reached!");
        }
        if (!PreWriteObject(writer, propertyClass)) {
            return writer;
        }

        _recursionLevel++;

        // Verbose mode requires us to write the size of the object. We'll write an empty size for now,
        // and go back to it later.
        var lengthLocation = writer.BitPos();
        if (Options.SerializerMode == Mode.Verbose) {
            writer.WriteUInt32(0);
        }

        switch (Options.SerializerMode) {
            case Mode.Compact:
                SerializeCompact(writer, propertyClass);
                break;
            case Mode.Verbose:
            default:
                SerializeVerbose(writer, propertyClass);
                break;
        }

        _recursionLevel--;

        // If we're not in verbose mode, we can return the writer now.
        if (Options.SerializerMode != Mode.Verbose) {
            return writer;
        }

        // If we're in verbose mode, we'll go back to the length location and write the size of the object.
        // The size of the object is the current bit position minus the length location, minus 32 bits for the
        // property hash. We'll then seek to the end of the object.
        var objectSize = (uint) (writer.BitPos() - lengthLocation);
        writer.SeekBit(lengthLocation);
        writer.WriteUInt32(objectSize);
        writer.SeekBit((int) (lengthLocation + objectSize));

        return writer;
    }

    private void SerializeCompact(BitWriter writer, PropertyClass propertyClass) {
        // In compact mode, the serializer will pass through properties in order under a certain bit mask.
        // Lengths and property hashes are not included.
        var fields = GetPropertyClassFields(propertyClass);
        foreach (var field in fields) {
            SerializeObjectField(writer, propertyClass, field);
        }
    }

    private void SerializeVerbose(BitWriter writer, PropertyClass propertyClass) {
        // In verbose mode, the serializer will write property size and hashes.
        var fields = GetPropertyClassFields(propertyClass);
        foreach (var field in fields) {
            var flags = (PropertyFlags) field.GetCustomAttribute<PropertyAttribute>()!.Flags;
            var writerFunc = ClassElementWriters.TryGetWriter(field.FieldType);

            // Write the property size and hash.
            var preWriteBitSize = writer.BitPos();
            writer.WriteUInt32(0); // Placeholder for size.
            writer.WriteUInt32(field.GetCustomAttribute<PropertyAttribute>()!.Hash);

            // If the field is a list, we'll write each object individually.
            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>)) {
                var list = (ICollection?) field.GetValue(propertyClass);
                var count = list?.Count ?? 0;

                // Write the list count.
                WriteVectorCount(writer, count);

                // Write each object in the list.
                if (list is not null) {
                    foreach (var item in list) {
                        SerializeObjectValue(writer, field, item);
                    }
                }
            }
            else {
                // Write the object value.
                SerializeObjectValue(writer, field, field.GetValue(propertyClass)!);
            }

            // Determine how many bits we wrote and write the size of the property.
            var postWriteBitSize = writer.BitPos();
            var propertySize = (uint) (postWriteBitSize - preWriteBitSize);
            writer.SeekBit(preWriteBitSize);
            writer.WriteUInt32(propertySize);
            writer.SeekBit(postWriteBitSize);
        }
    }

    private void SerializeObjectField(BitWriter writer, PropertyClass propertyClass, FieldInfo field) {
        var fieldType = field.FieldType;

        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)) {
            // If this field is a list, we'll serialize each object individually.
            var list = (ICollection?) field.GetValue(propertyClass);

            var count = list?.Count ?? 0;
            WriteVectorCount(writer, count);

            // Can't serialize a list that isn't there.
            if (list is not null) {
                foreach (var item in list) {
                    SerializeObjectValue(writer, field, item);
                }
            }
        }
        else {
            // If this field is not a list, we'll serialize it as a normal object.
            var fieldVal = field.GetValue(propertyClass);
            SerializeObjectValue(writer, field, fieldVal!);
        }
    }

    private void SerializeObjectValue(BitWriter writer, FieldInfo field, object value) {
        var type = field.FieldType;

        // If this is a list, change the type to be the inner type.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) {
            type = type.GetGenericArguments()[0];
        }

        var flags = (PropertyFlags) field.GetCustomAttribute<PropertyAttribute>()!.Flags;
        var writerFunc = ClassElementWriters.TryGetWriter(type);

        // Serialize enum if field type is enum or the ProperyFlags indicate bits or enum.
        if ((flags & (PropertyFlags.Bits | PropertyFlags.Enum)) != 0 || type.IsEnum) {
            if (Options.BehaviorFlags.HasFlag(Behaviors.StringEnums)) {
                writer.WriteString(value.ToString()!);
            }
            else {
                writer.WriteInt32((int) value);
            }
        }
        // Serialize as a primitive data type.
        else if (writerFunc != null) {
            writerFunc.Invoke(writer, value);
        }
        // If the value is null, we'll just serialize the property hash.
        else if (value is null) {
            PreWriteObject(writer, null!);
        }
        // If we're here, it means that the value is another PropertyClass.
        else {
            SerializeInternal(writer, (PropertyClass) value);
        }
    }

    private void WriteVectorCount(BitWriter buffer, int count) {
        if (Options.BehaviorFlags.HasFlag(Behaviors.CompactLength)) {
            if (count >= 128) {
                buffer.WriteBit(1);
                buffer.WriteBits(count, 31);
            }
            else {
                buffer.WriteBit(0);
                buffer.WriteBits((byte) count, 7);
            }
        }
        else {
            buffer.WriteInt32(count);
        }
    }

    #endregion

    #region Deserialize

    /// <summary>
    /// Deserializes a byte array into a PropertyClass object.
    /// </summary>
    /// <param name="buffer">The byte array to deserialize.</param>
    /// <returns>The deserialized PropertyClass object.</returns>
    public PropertyClass? Deserialize(byte[] buffer) {
        // Configure to begin deserialization.
        _recursionLevel = 0;
        var reader = new BitReader(buffer);

        if (Options.BehaviorFlags.HasFlag(Behaviors.CompactLength)) {
            reader.WithCompactLengths();
        }

        // If the serializer flags are set to compress, we'll decompress the serialized data.
        if (Options.BehaviorFlags.HasFlag(Behaviors.Compress)) {
            reader = Decompress(reader);
            if (reader is null) {
                return null;
            }

            if (Options.BehaviorFlags.HasFlag(Behaviors.CompactLength)) {
                reader.WithCompactLengths();
            }
        }

        return DeserializeInternal(reader);
    }

    protected virtual bool PreloadObject(BitReader buffer, out PropertyClass? propClass) {
        var hash = buffer.ReadUInt32();
        if (hash == 0) {
            propClass = null;
            return false;
        }

        // Dispatch the hash to our type cache to see if we carry this type.
        propClass = TypeCache.Dispatch(hash);
        if (propClass is not null) {
            return true;
        }

        // If we couldn't dispatch the hash in our normal type cache, check the server types cache.
        propClass = ServerTypeCache.Dispatch(hash);
        if (propClass is not null) {
            return true;
        }
        else {
            // If a hash is here that we don't have a type for, log it.
            // It isn't too worrisome to miss a type. There are a lot of server types.
            //Logger.Debug("Could not find type for hash {hash}", Logger.Args(hash));
            return false;
        }
    }

    protected virtual BitReader? Decompress(BitReader reader) {
        var uncompressedLength = reader.ReadInt32();
        var decompressedData = Compression.Decompress(reader.GetData()[4..]);

        // If the decompressed data length does not match the recorded length, log it and return null.
        if (decompressedData.Length != uncompressedLength) {
            Logger.Error($"[{nameof(ObjectSerializer)}]: Decompressed data length does not match recorded length. "
                             + $"Expected {uncompressedLength} bytes, got {decompressedData.Length} bytes.");
            return null;
        }

        return new BitReader(decompressedData);
    }

    private PropertyClass? DeserializeInternal(BitReader buffer) {
        if (_recursionLevel >= RecursionLimit) {
            throw new Exception("Recursion limit reached!");
        }

        _recursionLevel++;

        // Determine if we should deserialize this object at all.
        if (!PreloadObject(buffer, out var propertyClass)) {
            _recursionLevel--;
            return propertyClass;
        }

        // If we're in verbose mode, we'll read the size of the object.
        var typeSize = (Options.SerializerMode == Mode.Verbose)
            ? buffer.ReadUInt32() - 32 // -32 from the length itself.
            : 0;

        var deserializedObject = Options.SerializerMode switch {
            Mode.Compact => DeserializeCompact(buffer, propertyClass!),
            _ => DeserializeVerbose(buffer, propertyClass!, typeSize)
        };

        _recursionLevel--;

        return deserializedObject;
    }

    private PropertyClass? DeserializeCompact(BitReader buffer, PropertyClass propertyClass) {
        // In compact mode, the serializer will pass through properties in order under a certain bit mask.
        // Lengths and property hashes are not included.
        var fields = GetPropertyClassFields(propertyClass);
        foreach (var field in fields) {
            DeserializeObjectField(buffer, propertyClass, field);
        }

        return propertyClass;
    }

    private PropertyClass? DeserializeVerbose(BitReader bufReader, PropertyClass propertyClass, uint size) {
        // In verbose mode, the serializer will read property size and hashes.
        var fieldDictionary = GetPropertyClassFields(propertyClass)
            .ToDictionary(x => x.GetCustomAttribute<PropertyAttribute>()!.Hash, x => x);

        var objectStart = bufReader.BitPos();
        while (bufReader.BitPos() - objectStart < size) {
            var propertyStart = bufReader.BitPos();
            var propertySize = bufReader.ReadUInt32();
            var propertyHash = bufReader.ReadUInt32();

            if (fieldDictionary.TryGetValue(propertyHash, out var fieldRecord)) {
                DeserializeObjectField(bufReader, propertyClass, fieldRecord);
            }
            else {
                var hexHash = propertyHash.ToString("X");
                Logger.Debug("No property with hash {0}(0x{1}) in PropertyClass {2} was found. Skipping by {3} bits.",
                    Logger.Args(propertyHash, hexHash, propertyClass.GetType().ToString().Split('+')[^1], propertySize));
            }

            bufReader.SeekBit((int) (propertyStart + propertySize));
        }

        // Seek to the end of the object.
        bufReader.SeekBit((int) (objectStart + size));
        return propertyClass;
    }

    private void DeserializeObjectField(BitReader reader, PropertyClass propertyClass, FieldInfo field) {
        if (field is null) {
            return;
        }

        // Get the type of the field. If it's a list, we'll deserialize each object individually.
        // otherwise, we'll just deserialize the object as normal.
        var fieldType = field.FieldType;
        if (fieldType.IsGenericType
            && fieldType.GetGenericTypeDefinition() == typeof(List<>)) {
            // Create a generic list and iterate through the elements.
            var fieldInnerType = fieldType.GetGenericArguments()[0];
            var vectorType = typeof(List<>).MakeGenericType(fieldInnerType);
            var vector = (IList) Activator.CreateInstance(vectorType)!;
            var vectorLength = ReadVectorCount(reader);

            // Iterate through the elements and deserialize them.
            for (int i = 0; i < vectorLength; i++) {
                var val = DeserializeObjectValue(reader, fieldInnerType);

                // For some reason, GID fails to be added to the generic list type.
                // @todo: fixme
                vector?.Add(fieldInnerType == typeof(GID) ? new GID((ulong) val) : val);
            }

            SetValue(propertyClass, field, vector!);
        }
        else {
            var val = DeserializeObjectValue(reader, field.FieldType);
            SetValue(propertyClass, field, val!);
        }
    }

    private object? DeserializeObjectValue(BitReader reader, Type type) {
        if (type.IsEnum) {
            // Read human-readable string if serializer bitflag is set.
            if (!Options.BehaviorFlags.HasFlag(Behaviors.StringEnums)) {
                return reader.ReadUInt32();
            }

            // String enums may contain c++ jargon.
            var str = reader.ReadString().ToString();
            str = str.Split("::")[^1];

            if (str == "*" || str == "") {
                return 0;
            }

            if (Enum.TryParse(type, str, out var obj)) {
                return obj;
            }

            //Logger.Error("Could not parse string enum of {0}", Logger.Args(str));
            return 0;
        }

        // If it's a primitive data type, we'll just read it as-is.
        var potentialReader = ClassElementReaders.TryGetReader(type);
        if (potentialReader is not null) {
            return potentialReader.Invoke(reader);
        }

        // If it's not a primitive data type, it's another PropertyClass.
        var subVal = DeserializeInternal(reader);

        // This is a failsafe. If we could not successfully deserialize the PropertyClass.
        if (subVal is null && Options.SerializerMode == Mode.Verbose) {
            // Just return null if the buffer doesn't have enough bits to read.
            if (reader.BitPos() + 32 > reader.GetData().Length * 8) {
                return null;
            }

            // Only seek back if the hash is not 0.
            reader.SeekBit(reader.BitPos() - 32);
            var hash = reader.ReadUInt32();
            if (hash != 0) {
                var skip = reader.ReadInt32();
                reader.SeekBit(reader.BitPos() + skip - 32);
            }
        }

        return subVal;
    }

    private void SetValue(object propertyClass, FieldInfo field, object fieldValue) {
        var fieldType = field.FieldType;
        if (fieldValue == null) {
            return;
        }

        if (fieldType.IsEnum) {
            var stringEnum = Options.BehaviorFlags.HasFlag(Behaviors.StringEnums);
            field.SetValue(propertyClass, stringEnum
                ? fieldValue
                : Enum.ToObject(fieldType, (uint) fieldValue));
            return;
        }

        if (typeof(PropertyClass).IsAssignableFrom(fieldType)) {
            field.SetValue(propertyClass, fieldValue);
            return;
        }

        switch (fieldType.Name) {
            case nameof(GID):
                field.SetValue(propertyClass, new GID((ulong) fieldValue));
                break;
            case nameof(Bui2):
                field.SetValue(propertyClass, new Bui2((byte) fieldValue));
                break;
            case nameof(Bui4):
                field.SetValue(propertyClass, new Bui4((byte) fieldValue));
                break;
            case nameof(Bui5):
                field.SetValue(propertyClass, new Bui5((byte) fieldValue));
                break;
            case nameof(Bui7):
                field.SetValue(propertyClass, new Bui7((byte) fieldValue));
                break;
            case nameof(S24):
                field.SetValue(propertyClass, new S24((S24) fieldValue));
                break;
            case nameof(U24):
                field.SetValue(propertyClass, new U24((U24) fieldValue));
                break;
            case nameof(Point):
                var pointCast = (Vector2) (fieldValue);
                field.SetValue(propertyClass, new Point((int) pointCast.X, (int) pointCast.Y));
                break;
            case nameof(ByteString):
                field.SetValue(propertyClass, new ByteString(fieldValue.ToString()!));
                break;
            case nameof(WideByteString):
                field.SetValue(propertyClass, new WideByteString(fieldValue.ToString()!));
                break;
            default:
                try {
                    var convertedVal = Convert.ChangeType(fieldValue, fieldType);
                    field.SetValue(propertyClass, convertedVal);
                }
                catch (Exception) {
                    Logger.Error($"Could not convert field {fieldType.Name} " +
                                     $"In PropertyClass {propertyClass.GetType()}.");
                }
                break;
        }
    }

    private uint ReadVectorCount(BitReader buffer) {
        if (Options.BehaviorFlags.HasFlag(Behaviors.CompactLength)) {
            // If the MSB is 1, we're still using the regular length.
            return buffer.ReadBits<uint>(buffer.ReadBit() ? 31 : 7);
        }
        else {
            return buffer.ReadUInt32();
        }
    }

    #endregion

    private IEnumerable<FieldInfo> GetPropertyClassFields(PropertyClass propClass)
    {
        if (propClass is null)
        {
            throw new ArgumentNullException(nameof(propClass));
        }

        return propClass.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
            .Where(x => Attribute.IsDefined(x, typeof(PropertyAttribute)))
            .Select(x => (Field: x, Attribute: x.GetCustomAttribute<PropertyAttribute>()))
            .Where(x => x.Attribute is not null &&
                        ((PropertyFlags) x.Attribute!.Flags & PropertyFlags.Deprecated) == 0 &&
                        ((PropertyFlags) x.Attribute!.Flags & Options.PropertyMask) == Options.PropertyMask &&
                        !(Options.BehaviorFlags.HasFlag(Behaviors.AlwaysEncode) && ((PropertyFlags) x.Attribute!.Flags & PropertyFlags.Encode) != 0))
            .OrderBy(x => x.Field, new PropertyClassFieldComparer())
            .Select(x => x.Field);
    }

    private class PropertyClassFieldComparer : IComparer<FieldInfo?> {
        public int Compare(FieldInfo? x, FieldInfo? y) {
            if (x?.DeclaringType != y?.DeclaringType) {
                return x?.DeclaringType?.IsAssignableFrom(y?.DeclaringType) ?? false ? -1 : 1;
            }
            return x?.MetadataToken.CompareTo(y?.MetadataToken) ?? 0;
        }
    }
}
