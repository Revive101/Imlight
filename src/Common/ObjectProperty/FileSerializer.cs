/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using System.Xml;
using Imlight.Common.Caches;
using Imlight.Common.Cryptography;
using Imlight.Common.Formats;
using Imlight.Common.IO;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Ionic.Zlib;

namespace Imlight.Common.ObjectProperty;

/// <summary>
/// An <see cref="ObjectSerializer"/> type that allows further abstraction in deserializing KIWAD internals.
/// </summary>
public class FileSerializer : ObjectSerializer {
    private const uint BiNdMagic = 0x644E4942;
    private const uint BiNdPropertyFlags = 7;
    private const int BiNdHeaderSize = 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSerializer"/> class with default options.
    /// </summary>
    public FileSerializer() {
        base.Options.SerializerMode = SerializerOptions.Mode.Verbose;
        base.Options.BehaviorFlags = SerializerOptions.Behaviors.UseFlags | SerializerOptions.Behaviors.CompactLength;
        base.Options.PropertyMask = (SerializerOptions.PropertyFlags) BiNdPropertyFlags;
    }

    /// <summary>
    /// Returns a new instance of the FileSerializer with the specified SerializerOptions.Mode.
    /// </summary>
    /// <param name="mode">The SerializerOptions.Mode to set.</param>
    /// <returns>A new instance of the FileSerializer with the specified SerializerOptions.Mode.</returns>
    public override FileSerializer OnMode(SerializerOptions.Mode mode) {
        this.Options.SerializerMode = mode;
        return this;
    }

    /// <summary>
    /// Returns a new instance of the FileSerializer with the specified SerializerOptions.Behaviors flags.
    /// </summary>readbits
    /// <param name="flags">The SerializerOptions.Behaviors flags to set.</param>
    /// <returns>A new instance of the FileSerializer with the specified SerializerOptions.Behaviors flags.</returns>
    public override FileSerializer OnBehaviors(SerializerOptions.Behaviors flags) {
        this.Options.BehaviorFlags = flags;
        return this;
    }

    /// <summary>
    /// Returns a new instance of the FileSerializer with the specified property flags.
    /// </summary>
    /// <param name="flags">The property flags to set.</param>
    /// <returns>A new instance of the FileSerializer with the specified property flags.</returns>
    public override FileSerializer OnPropertyMask(SerializerOptions.PropertyFlags flags) {
        this.Options.PropertyMask = flags;
        return this;
    }

    /// <summary>
    /// Opens and attempts deserialization of a file inside a KIWAD.
    /// </summary>
    /// <param name="wad">The KIWAD in question.</param>
    /// <param name="file">The name of the file inside the KIWAD to attempt decoding.</param>
    /// <typeparam name="T">The expected return type. If unknown, simply <see cref="PropertyClass"/>.</typeparam>
    /// <returns>The decoded type derived from <see cref="PropertyClass"/>.</returns>
    public T? OpenClass<T>(KiWad wad, string file) where T : PropertyClass {
        using var stream = wad?.OpenFile(file);
        if (stream == null) {
            return null;
        }

        return DeserializeFromStream<T>(stream);
    }

    /// <summary>
    /// Attempts deserialization from an existing <see cref="MemoryStream"/>.
    /// </summary>
    /// <param name="stream">The stream pointing to the binary data to attempt decoding.</param>
    /// <param name="isSimpleBinary">Ticked if the file ends in ".bin"</param>
    /// <typeparam name="T">The expected return type. If unknown, simply <see cref="PropertyClass"/>.</typeparam>
    /// <returns>The decoded type derived from <see cref="PropertyClass"/>.</returns>
    public T? OpenClass<T>(MemoryStream stream) where T : PropertyClass {
        return DeserializeFromStream<T>(stream);
    }

    protected override BitReader? Decompress(BitReader reader) {
        var decompressedSize = reader.ReadUInt32();
        var outBytes = new byte[decompressedSize];

        using (var stream = new ZlibStream(new MemoryStream(reader.GetRelativeData()), CompressionMode.Decompress)) {
            var read = stream.Read(outBytes);
            if (read != decompressedSize) {
                throw new EndOfStreamException($"Decompress: expected {decompressedSize} bytes, got {read}");
            }
        }

        return new BitReader(outBytes);
    }

    private T? DeserializeFromStream<T>(MemoryStream stream) where T : PropertyClass {
        Span<byte> buffer = stackalloc byte[BiNdHeaderSize];
        var read = stream.Read(buffer);

        // Validate that there is even enough data here.
        if (read != BiNdHeaderSize) {
            return null;
        }

        var header = BitConverter.ToUInt32(buffer);
        if (header == BiNdMagic) {
            // If this is BINd_MAGIC, the flags are next.
            var reader = new BitReader(stream);
            var flags = reader.ReadUInt32();
            if ((flags & 8) != 0) {
                _ = reader.ReadBit();
            }

            // Set this serializer's behaviors to match the flags we just read.
            this.OnBehaviors((SerializerOptions.Behaviors) flags);

            return DeserializeAndHandleErrors<T>(reader.GetRelativeData());
        }
        else {
            // If this is not BiNd, we'll try to read it the same, just without the BiNd header and flags.
            stream.Seek(0, SeekOrigin.Begin);
            return DeserializeAndHandleErrors<T>(stream.ToArray());
        }
    }

    private T? DeserializeAndHandleErrors<T>(byte[] data) where T : PropertyClass {
        try {
            var val = Deserialize(data);
            if (val is null) {
                return default;
            }
            else {
                return (T) val;
            }
        }
        catch {
            // Log the exception.
            Logger.Error("Failed to deserialize {0}", Logger.Args(typeof(T).Name));
            return null;
        }
    }

    private static T? ReadXml<T>(XmlDocument doc) where T : PropertyClass {
        if (doc is null) {
            throw new ArgumentNullException(nameof(doc));
        }

        var xmlObjects = GetSerializedXmlObjects(doc);
        if (xmlObjects is null) {
            return null;
        }

        var nameAttr = xmlObjects.Attributes?.GetNamedItem("Name");
        var objName = "";
        if (nameAttr != null) {
            objName = nameAttr.Value;
        }
        else if (xmlObjects.LocalName.StartsWith("class.")) {
            objName = $"class {xmlObjects.LocalName[6..]}";
        }

        var objHash = StringHash.Compute(objName!);
        var propClass = TypeCache.Dispatch(objHash);

        return (T) propClass;
    }

    private static XmlNode? GetSerializedXmlObjects(XmlNode doc) {
        var objCollection = doc["Objects"];
        if (objCollection is null) {
            //throw new InvalidOperationException("Cannot find Object Collection!");
            return null;
        }

        return objCollection;
    }
}
