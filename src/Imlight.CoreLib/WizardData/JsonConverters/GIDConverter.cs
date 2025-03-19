/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Newtonsoft.Json;
using Imcodec.Types;
using System;

namespace Imlight.CoreLib.WizardData.JsonConverters; 

public class GIDConverter : JsonConverter {

    public override bool CanConvert(Type objectType) 
        => objectType == typeof(GID);

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Null) {
            return default(GID);
        }

        if (reader.TokenType == JsonToken.Integer) {
            if (reader.Value is long longValue) {
                return (GID) (ulong) longValue;
            }

            if (reader.Value is int intValue) {
                return (GID) (ulong) intValue;
            }

            // If it's already a ulong, just convert it.
            if (reader.Value is ulong ulongValue) {
                return (GID) ulongValue;
            }
        }

        throw new JsonSerializationException(
            $"Unexpected token type: {reader.TokenType} with value {reader.Value} of type {reader.Value?.GetType().Name ?? "null"}"
        );
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
        if (value is GID gid) {
            writer.WriteValue(gid.Full);
        }
        else {
            writer.WriteNull();
        }
    }

}