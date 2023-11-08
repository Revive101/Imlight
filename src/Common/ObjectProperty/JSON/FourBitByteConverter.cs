/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Newtonsoft.Json;

namespace Imlight.Common.ObjectProperty.JSON;

public class FourBitByteConverter : JsonConverter {
    public override bool CanConvert(Type objectType) {
        return objectType == typeof(Bui4);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value is Bui4 fourBitByte) {
            writer.WriteValue(fourBitByte.Value);
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
        if (reader.Value != null && byte.TryParse(reader.Value.ToString(), out byte byteValue)) {
            return new Bui4(byteValue);
        }

        return null;
    }
}
