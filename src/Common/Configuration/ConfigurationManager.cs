/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Imlight.Common.Configuration;

public static class ConfigurationManager {
    private static readonly string s_path = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        ?? string.Empty, "Config/Imlight.ini");

    private static ServerSettings? s_settings;
    public static ServerSettings Settings => s_settings ?? LoadOrCreateServerSettings();

    private static ServerSettings LoadOrCreateServerSettings() {
        if (!File.Exists(s_path)) {
            return CreateDefaultServerSettings();
        }

        var iniData = File.ReadAllText(s_path);
        var deserializedData = IniSerializer.Deserialize<ServerSettings>(iniData);

        s_settings = deserializedData;

        return deserializedData;
    }

    private static ServerSettings CreateDefaultServerSettings() {
        // Set the values of each property to the default value.
        var defaultSettings = new ServerSettings();
        var properties = typeof(ServerSettings).GetProperties();
        foreach (var property in properties) {
            if (property.GetCustomAttributes(typeof(DefaultValueAttribute), false)
                    .FirstOrDefault() is not DefaultValueAttribute attribute) {
                continue;
            }

            // Convert the value to the correct type.
            var value = Convert.ChangeType(attribute.Value, property.PropertyType);
            property.SetValue(defaultSettings, value);
        }

        // Try to serialize the object and write it to the file.
        // It's fine if we can't find a file at that path, just keep our default settings.
        try {
            var serializedData = IniSerializer.Serialize(defaultSettings);
            File.WriteAllText(s_path, serializedData);
        }
        catch { }
        finally {
            s_settings = defaultSettings;
        }

        return defaultSettings;
    }
}
