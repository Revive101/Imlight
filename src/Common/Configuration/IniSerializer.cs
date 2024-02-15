/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text;

namespace Imlight.Common.Configuration;

public static class IniSerializer {
    /// <summary>
    /// Serializes an object to an INI file format.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>A string containing the serialized INI data.</returns>
    public static string Serialize<T>([NotNull]T obj) where T : new() {
        var iniData = new StringBuilder();
        var type = obj!.GetType();

        // If the object has a section attribute, add it to the top of the file.
        var classSectionAttr = type.GetCustomAttribute<IniSectionAttribute>();
        if (classSectionAttr != null) {
            iniData.AppendLine($"[{classSectionAttr.SectionName}]");
        }

        foreach (var property in type.GetProperties()) {
            var sectionAttribute = property.GetCustomAttribute<IniSectionAttribute>();
            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();
            var descriptionAttribute = property.GetCustomAttribute<DescriptionAttribute>();

            // Write the section header if it exists.
            if (sectionAttribute is not null) {
                iniData.AppendLine($"\n[{sectionAttribute.SectionName}]");
            }

            // Write the description if it exists.
            if (descriptionAttribute is not null) {
                iniData.AppendLine($"; {descriptionAttribute.Description}");
            }

            var value = property.GetValue(obj) ?? defaultValueAttribute?.Value;
            iniData.AppendLine($"{property.Name} = {value}");
        }

        return iniData.ToString();
    }

    /// <summary>
    /// Deserializes the specified INI data into an instance of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize the INI data into.</typeparam>
    /// <param name="iniData">The INI data to deserialize.</param>
    /// <returns>An instance of the specified type with its properties set according to the values in the INI data.</returns>
    public static T Deserialize<T>(string iniData)
        where T : new() {
        var obj = new T();
        var propertyValues = new Dictionary<string, string>();

        using (var reader = new StringReader(iniData)) {
            while (reader.ReadLine() is { } line) {
                line = line.Trim();
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#")) {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]")) {
                    // Skip section headers. It's just for readability.
                    continue;
                }

                // Split the line into a key/value pair.
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2) {
                    propertyValues[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        var type = typeof(T);

        foreach (var property in type.GetProperties()) {
            propertyValues.TryGetValue(property.Name, out var value);

            if (value == null) {
                continue;
            }

            property.SetValue(obj, Convert.ChangeType(value, property.PropertyType));
        }

        return obj;
    }
}
