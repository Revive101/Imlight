/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Newtonsoft.Json;

namespace Imlight.Server.Shared.WizardData;

public class ULongToStringConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(ulong);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        return reader.TokenType switch
        {
            JsonToken.String when ulong.TryParse(reader.Value.ToString(), out ulong result) => result,
            JsonToken.Integer => Convert.ToUInt64(reader.Value),
            _ => throw new JsonSerializationException($"Unable to convert '{reader.TokenType}' to {objectType.Name}.")
        };
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }
}