using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Imlight.Common.Serializable;
using Newtonsoft.Json;

namespace Imlight.Common.Configuration;

public static class ConfigurationManager
{
    private static readonly string _path = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        ?? string.Empty, "Config/Imlight.ini");

    private static ServerSettings _settings;
    public static ServerSettings Settings => _settings ?? LoadOrCreateServerSettings();

    private static ServerSettings LoadOrCreateServerSettings()
    {
        if (!File.Exists(_path))
            return CreateDefaultServerSettings();

        var iniData = File.ReadAllText(_path);
        var deserializedData = IniSerializer.Deserialize<ServerSettings>(iniData);

        return deserializedData;
    }

    private static ServerSettings CreateDefaultServerSettings()
    {
        // Set the values of each property to the default value.
        var defaultSettings = new ServerSettings();
        var properties = typeof(ServerSettings).GetProperties();
        foreach (var property in properties)
        {
            if (property.GetCustomAttributes(typeof(DefaultValueAttribute), false)
                    .FirstOrDefault() is not DefaultValueAttribute attribute)
                continue;

            // Convert the value to the correct type.
            var value = Convert.ChangeType(attribute.Value, property.PropertyType);
            property.SetValue(defaultSettings, value);
        }
        
        // Serialize the data and write it to the file.
        var serializedData = IniSerializer.Serialize(defaultSettings);
        File.WriteAllText(_path, serializedData);
        
        return defaultSettings;
    }
}