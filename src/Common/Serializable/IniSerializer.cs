using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using Imlight.Common.Configuration;
using DefaultValueAttribute = Imlight.Common.Configuration.DefaultValueAttribute;

namespace Imlight.Common.Serializable;

public static class IniSerializer
{
    public static string Serialize<T>(T obj) 
        where T : new()
    {
        var iniData = new StringBuilder();
        var type = obj.GetType();
        
        // If the object has a section attribute, add it to the top of the file.
        var classSectionAttr = type.GetCustomAttribute<IniSectionAttribute>();
        if (classSectionAttr != null)
            iniData.AppendLine($"[{classSectionAttr.SectionName}]");

        foreach (var property in type.GetProperties())
        {
            var sectionAttribute = property.GetCustomAttribute<IniSectionAttribute>();
            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();

            if (sectionAttribute != null)
                iniData.AppendLine($"\n[{sectionAttribute.SectionName}]");

            var value = property.GetValue(obj) ?? defaultValueAttribute?.Value;
            iniData.AppendLine($"{property.Name} = {value}");
        }

        return iniData.ToString();
    }

    public static T Deserialize<T>(string iniData) 
        where T : new()
    {
        var obj = new T();
        var propertyValues = new Dictionary<string, string>();

        using (var reader = new StringReader(iniData))
        {
            while (reader.ReadLine() is { } line)
            {
                line = line.Trim();
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    // Skip section headers. It's just for readability.
                    continue;
                }

                // Split the line into a key/value pair.
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    propertyValues[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        var type = typeof(T);

        foreach (var property in type.GetProperties())
        {
            propertyValues.TryGetValue(property.Name, out var value);
            property.SetValue(obj, Convert.ChangeType(value, property.PropertyType));
        }

        return obj;
    }
}