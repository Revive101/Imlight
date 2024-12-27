/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common.ObjectProperty.PropertyReflection;
using Newtonsoft.Json;
using System;

namespace Imlight.Common.ObjectProperty.JSON;

public class GIDConverter : JsonConverter<GID> {
    public override GID ReadJson(JsonReader reader, Type objectType, GID existingValue, bool hasExistingValue, JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Integer) {
            var value = Convert.ToUInt64(reader.Value);
            return new GID(value);
        }

        throw new JsonSerializationException($"Unexpected token type {reader.TokenType} when parsing GID");
    }

    public override void WriteJson(JsonWriter writer, GID value, JsonSerializer serializer) {
        writer.WriteValue(value.Full);
    }
}