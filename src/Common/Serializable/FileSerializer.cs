/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using System.Xml;
using Imlight.Common.Cryptography;
using Imlight.Common.Formats;
using Imlight.Common.IO;
using Imlight.Common.Serializable.ObjectProperty;
using Ionic.Zlib;
using WizUnraveler.Cache;

namespace Imlight.Common.Serializable;

/// <summary>
/// An <see cref="ObjectSerializer"/> type that allows further abstraction in deserializing KIWAD internals.
/// </summary>
public class FileSerializer : ObjectSerializer
{
    private const uint BiNdMagic = 0x644E4942;
    private const uint BiNdPropertyFlags = 7;
    private const int BiNdHeaderSize = 4;

    /// <summary>
    /// Creates a new <see cref="FileSerializer"/> with default BiND flags.
    /// </summary>
    public FileSerializer() : base(Mode.Verbose, 
        options: SerializerFlags.UseFlags | SerializerFlags.CompactLength, 
        propertyFlagMask: PropertyFlags.Encode) { }
        
    public override FileSerializer WithMode(Mode mode)
    {
        this.SerializerMode = mode;
        return this;
    }

    public override FileSerializer WithSerializerFlags(SerializerFlags flags)
    {
        this.Options = flags;
        return this;
    }

    public override FileSerializer WithPropertyFlags(PropertyFlags flags)
    {
        this.PropertyMask = flags;
        return this;
    }

    /// <summary>
    /// Opens and attempts deserialization of a file inside a KIWAD.
    /// </summary>
    /// <param name="wad">The KIWAD in question.</param>
    /// <param name="file">The name of the file inside the KIWAD to attempt decoding.</param>
    /// <typeparam name="T">The expected return type. If unknown, simply <see cref="PropertyClass"/>.</typeparam>
    /// <returns>The decoded type derived from <see cref="PropertyClass"/>.</returns>
    public T OpenClass<T>(Wad wad, string file) 
        where T : PropertyClass
    {
        using var stream = wad?.OpenFile(file);
        if (stream == null) 
            return null;
            
        return GetFileExtension(file) == "bin" 
            ? LoadSimpleBinary<T>(stream) 
            : Load<T>(stream);
    }

    /// <summary>
    /// Attempts deserialization from an existing <see cref="MemoryStream"/>.
    /// </summary>
    /// <param name="stream">The stream pointing to the binary data to attempt decoding.</param>
    /// <param name="isSimpleBinary">Ticked if the file ends in ".bin"</param>
    /// <typeparam name="T">The expected return type. If unknown, simply <see cref="PropertyClass"/>.</typeparam>
    /// <returns>The decoded type derived from <see cref="PropertyClass"/>.</returns>
    public T OpenClass<T>(MemoryStream stream, bool isSimpleBinary = false)
        where T : PropertyClass
    {
        // Unfortunately, we must break SOLID code practices and pass a boolean parameter. If there was any other
        // way, I would do it. But there is not.
        return isSimpleBinary 
            ? LoadSimpleBinary<T>(stream) 
            : Load<T>(stream);
    }

    private T Load<T>(Stream stream) 
        where T : PropertyClass
    {
        Span<byte> buffer = stackalloc byte[BiNdHeaderSize];
        var read = stream.Read(buffer);
        if (read != BiNdHeaderSize)
            return null;

        var bind = BitConverter.ToUInt32(buffer);
        stream.Position -= BiNdHeaderSize;

        // If this is BINd_MAGIC, reading it is trivial. It's just a serialized PropertyClass.
        if (bind == BiNdMagic)
            return ReadBiNd<T>(stream);

        // If it's not BINd_MAGIC, it's in binary XML.
        /*
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(stream);
            var xml = ReadXml<T>(xmlDoc);
            return xml;
        }
        catch (Exception ex)
        {
            if (ex is XmlException || ex is InvalidOperationException || ex is IOException)
            {
                return null;
            }

            // Throw any other unhandled exception.
            throw;
        }
        */

        return null;
    }

    private T LoadSimpleBinary<T>(Stream stream)
        where T : PropertyClass
    {
        using var buffer = new BitIterator(stream);

        WithMode(Mode.Verbose)
            .WithSerializerFlags(SerializerFlags.None)
            .WithPropertyFlags((PropertyFlags)BiNdPropertyFlags);
        return (T)Deserialize(buffer);
    }

    private T ReadBiNd<T>(Stream stream) 
        where T : PropertyClass
    {
        using var buffer = new BitIterator(stream);

        var bind = buffer.ReadUInt32();
        if (bind != BiNdMagic)
            throw new InvalidDataException("This stream does not contain the BINd_MAGIC header!");

        // Decompress if bit flag is set.
        var flags = buffer.ReadUInt32();
        BitIterator objectBuffer = null;
        if ((flags & 8) != 0)
        {
            var isCompressed = buffer.ReadBit();
            if (isCompressed)
            {
                objectBuffer = InnerInflate(buffer);
            }
        }
        objectBuffer ??= buffer;

        PropertyMask = (PropertyFlags)BiNdPropertyFlags;
        return (T)Deserialize(objectBuffer);
    }

    private static T ReadXml<T>(XmlDocument doc) 
        where T : PropertyClass
    {
        if (doc is null) 
            throw new ArgumentNullException(nameof(doc));

        var xmlObjects = GetSerializedXmlObjects(doc);
        if (xmlObjects is null) 
            return null;

        var nameAttr = xmlObjects.Attributes?.GetNamedItem("Name");
        var objName = "";
        if (nameAttr != null)
        {
            objName = nameAttr.Value;
        }
        else if (xmlObjects.LocalName.StartsWith("class."))
        {
            objName = $"class {xmlObjects.LocalName[6..]}";
        }

        var objHash = StringHash.Compute(objName);
        var propClass = TypeCache.Dispatch(objHash);

        return (T)propClass;
    }

    private static XmlNode GetSerializedXmlObjects(XmlNode doc)
    {
        var objCollection = doc["Objects"];
        if (objCollection is null)
            //throw new InvalidOperationException("Cannot find Object Collection!");
            return null;

        return objCollection;
    }
        
    private static string GetFileExtension(string file)
    {
        var ext = Path.GetExtension(file);
        return ext?[1..];
    }
        
    private static byte[] InnerDeflate(byte[] bytes)
    {
        var compressed = ZlibStream.CompressBuffer(bytes);
        using var memStream = new MemoryStream(compressed.Length + 4);
        using var writer = new BinaryWriter(memStream);
        writer.Write(bytes.Length);
        writer.Write(compressed);
        return memStream.GetBuffer();
    }

    private static BitIterator InnerInflate(BitIterator buffer)
    {
        var decompressedSize = buffer.ReadUInt32();
        var outBytes = new byte[decompressedSize];
        using (var stream = new ZlibStream(buffer.GetCurrentStream(), CompressionMode.Decompress))
        {
            var read = stream.Read(outBytes);
            if (read != decompressedSize) throw new EndOfStreamException($"Decompress: expected {decompressedSize} bytes, got {read}");
        }

        return new BitIterator(outBytes);
    }
}