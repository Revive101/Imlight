/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Newtonsoft.Json;

namespace Imlight.Common.ObjectProperty.JSON;

public class ULongWordConverter : JsonConverter {
    public override bool CanConvert(Type objectType) {
        return objectType == typeof(U24);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value is U24 ulongWord) {
            writer.WriteValue(ulongWord.Value);
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        if (reader.Value != null && uint.TryParse(reader.Value.ToString(), out uint uintValue)) {
            return new U24(uintValue);
        }

        return null;
    }
}
