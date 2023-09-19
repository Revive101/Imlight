/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Newtonsoft.Json;

namespace Imlight.Common.Serializable.ObjectProperty.JSON;

public class LongWordConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(LongWord);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is LongWord longWord)
        {
            writer.WriteValue(longWord.Value);
        }
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        if (reader.Value != null && int.TryParse(reader.Value.ToString(), out int intValue))
        {
            return new LongWord(intValue);
        }
        return null;
    }
}