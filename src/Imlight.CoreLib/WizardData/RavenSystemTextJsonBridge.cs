/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Raven.Client.Documents.Conventions;
using Raven.Client.Json.Serialization;
using Raven.Client.Json.Serialization.NewtonsoftJson;

namespace Imlight.CoreLib.WizardData;

internal sealed class RavenSystemTextJsonBridge : Newtonsoft.Json.JsonConverter {
    public static DocumentConventions Apply(DocumentConventions conventions) {
        var serialization = (NewtonsoftJsonSerializationConventions) conventions.Serialization;
        serialization.JsonContractResolver = new IgnoreResolver(serialization);
        serialization.CustomizeJsonSerializer += s => {
            s.Converters.Add(new RavenSystemTextJsonBridge());
            s.Converters.Add(new BigIntegerConverter());
        };
        serialization.CustomizeJsonDeserializer += s => {
            s.Converters.Add(new RavenSystemTextJsonBridge());
            s.Converters.Add(new BigIntegerConverter());
        };

        return conventions;
    }

    public override bool CanConvert(Type objectType) {
        objectType = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return objectType.GetCustomAttribute<System.Text.Json.Serialization.JsonConverterAttribute>() != null;
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer) {
        var token = JToken.Load(reader);
        if (token.Type == JTokenType.Null) {
            return null;
        }

        var type = Nullable.GetUnderlyingType(objectType) ?? objectType;

        return System.Text.Json.JsonSerializer.Deserialize(token.ToString(Formatting.None), type);
    }

    public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            
            return;
        }

        // Tokens only: Raven's writers do not support WriteRawValue.
        var token = JToken.Parse(System.Text.Json.JsonSerializer.Serialize(value, value.GetType()));
        NormalizeTokens(token);
        token.WriteTo(writer, [.. serializer.Converters]);
    }

    private static void NormalizeTokens(JToken token) {
        switch (token) {
            case JValue value:
                if (value.Value is BigInteger bi) {
                    if (bi >= 0 && bi <= ulong.MaxValue) {
                        value.Value = (ulong) bi;
                    }
                    else if (bi >= long.MinValue && bi <= long.MaxValue) {
                        value.Value = (long) bi;
                    }
                    else {
                        value.Value = bi.ToString();
                    }
                }
                break;

            case JContainer container:
                foreach (var child in container.Children()) {
                    NormalizeTokens(child);
                }
                break;
        }
    }

    private sealed class BigIntegerConverter : Newtonsoft.Json.JsonConverter<BigInteger> {
        public override void WriteJson(JsonWriter writer, BigInteger value, Newtonsoft.Json.JsonSerializer serializer) {
            if (value >= 0 && value <= ulong.MaxValue) {
                writer.WriteValue((ulong) value);
            }
            else if (value >= long.MinValue && value <= long.MaxValue) {
                writer.WriteValue((long) value);
            }
            else {
                writer.WriteValue(value.ToString());
            }
        }

        public override BigInteger ReadJson(JsonReader reader, Type objectType, BigInteger existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer) {
            var token = reader.Value?.ToString();
            return string.IsNullOrEmpty(token) ? BigInteger.Zero : BigInteger.Parse(token);
        }
    }

    private sealed class IgnoreResolver(ISerializationConventions conventions) : DefaultRavenContractResolver(conventions) {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType) 
            => [.. base.GetSerializableMembers(objectType)
                       .Where(m => m.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always)];
    }

}
