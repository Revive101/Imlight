/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Newtonsoft.Json;

namespace Imlight.Common.ObjectProperty.JSON;

public class LongWordConverter : JsonConverter {
    public override bool CanConvert(Type objectType) {
        return objectType == typeof(S24);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value is S24 longWord) {
            writer.WriteValue(longWord.Value);
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        if (reader.Value != null && int.TryParse(reader.Value.ToString(), out int intValue)) {
            return new S24(intValue);
        }

        return null;
    }
}
