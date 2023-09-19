/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Text;
using Newtonsoft.Json;

namespace Imlight.Common.IO;

[JsonConverter(typeof(ByteStringConverter))]
public struct ByteString
{
    private readonly byte[] _bytes;

    public ByteString(byte[] bytes)
    {
        _bytes = bytes;
    }

    public ByteString(string toString)
    {
        _bytes = Encoding.UTF8.GetBytes(toString);
    }

    public static implicit operator string(ByteString byteString)
    {
        return byteString._bytes is null 
            ? string.Empty 
            : Encoding.UTF8.GetString(byteString._bytes);
    }

    public static implicit operator ByteString(string str)
    {
        return new ByteString(Encoding.UTF8.GetBytes(str));
    }

    public static implicit operator byte[](ByteString byteString)
    {
        return byteString._bytes;
    }

    public static implicit operator ByteString(byte[] buffer)
    {
        return new ByteString(buffer);
    }

    public override string ToString()
    {
        return _bytes is null ? null : Encoding.UTF8.GetString(_bytes);
    }

    public int Length
    {
        get
        {
            if (_bytes is null)
                return 0;

            return _bytes.Length;
        }
    }
}
    
public class ByteStringConverter : JsonConverter<ByteString>
{
    public override void WriteJson(JsonWriter writer, ByteString value, JsonSerializer serializer)
    {
        string stringValue = value;
        writer.WriteValue(stringValue);
    }

    public override ByteString ReadJson(JsonReader reader, Type objectType, ByteString existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.Value is string stringValue)
        {
            return new ByteString(stringValue);
        }

        throw new JsonSerializationException("Unable to deserialize ByteString.");
    }

    public override bool CanRead => true;

    public override bool CanWrite => true;
}