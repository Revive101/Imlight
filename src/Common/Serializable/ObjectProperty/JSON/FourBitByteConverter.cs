/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Newtonsoft.Json;

namespace Imlight.Common.Serializable.ObjectProperty.JSON;

public class FourBitByteConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(FourBitByte);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is FourBitByte fourBitByte)
        {
            writer.WriteValue(fourBitByte.Value);
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.Value != null && byte.TryParse(reader.Value.ToString(), out byte byteValue))
        {
            return new FourBitByte(byteValue);
        }
        return null;
    }
}