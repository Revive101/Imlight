using Imcodec.ObjectProperty;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Imlight.CoreLib.WizardData;

public static class SpiralJsonOptions {

    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions() {
        var resolver = new DefaultJsonTypeInfoResolver();

        // Find classes which are not abstract and inherit from PropertyClass
        var imcodecAssembly = typeof(PropertyClass).Assembly;
        var assemblyName = imcodecAssembly.GetName().Name;

        var concreteTypes = imcodecAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(PropertyClass).IsAssignableFrom(t))
            .ToList();

        resolver.Modifiers.Add(typeInfo => {
            if (!typeof(PropertyClass).IsAssignableFrom(typeInfo.Type) || typeInfo.Type.IsSealed) {
                return;
            }

            var derivedTypes = concreteTypes
                .Where(t => t != typeInfo.Type && typeInfo.Type.IsAssignableFrom(t))
                .ToList();

            if (derivedTypes.Count == 0) {
                return;
            }

            // tell System.Json to read "$type$ property to set the type of the object
            var poly = new JsonPolymorphismOptions {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType
            };

            if (!typeInfo.Type.IsAbstract) {
                poly.DerivedTypes.Add(new JsonDerivedType(typeInfo.Type, $"{typeInfo.Type.FullName}, {assemblyName}"));
            }

            foreach (var derived in derivedTypes) {
                poly.DerivedTypes.Add(new JsonDerivedType(derived, $"{derived.FullName}, {assemblyName}"));
            }

            typeInfo.PolymorphismOptions = poly;
        });

        return new JsonSerializerOptions {
            TypeInfoResolver = resolver,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = {
                new JsonStringEnumConverter(),
            }
        };
    }

}
