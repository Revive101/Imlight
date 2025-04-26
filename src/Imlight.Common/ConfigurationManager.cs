/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * CONFIGURATION MANAGEMENT 
 * ========================================================================
 * 
 * PURPOSE:
 * Provides a robust, flexible configuration management solution for reading 
 * and parsing INI configuration files with advanced type conversion features.
 * 
 * USAGE EXAMPLE:
 * ConfigurationManager.Initialize("path/to/config.ini");
 * var serverPort = ConfigurationManager.Settings["Server.Port"].AsInt();
 * var serverName = ConfigurationManager.Settings["Server.Name"].AsString();
 * 
 * NOTE:
 * Ensure ConfigurationManager.Initialize() is called before accessing settings.
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.IO;

namespace Imlight.Common;

/// <summary>
/// Manages configuration settings by reading and parsing INI configuration files.
/// </summary>
public static class ConfigurationManager {

    private static readonly Dictionary<string, string> s_settings = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Dictionary<string, string>> s_sections = new(StringComparer.OrdinalIgnoreCase);
    private static bool s_isInitialized = false;
    private static string s_configFilePath = "";

    /// <summary>
    /// Provides indexer access to configuration settings.
    /// </summary>
    public static ConfigSettings Settings { get; } = new();

    /// <summary>
    /// Initializes the configuration manager with the specified INI file.
    /// </summary>
    /// <param name="iniFilePath">Path to the INI file</param>
    public static void Initialize(string iniFilePath) {
        if (!File.Exists(iniFilePath)) {
            throw new FileNotFoundException($"Configuration file not found: {iniFilePath}");
        }

        s_configFilePath = iniFilePath;
        LoadConfiguration();
        s_isInitialized = true;
    }

    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The configuration value.</returns>
    public static string? GetSetting(string key) {
        EnsureInitialized();

        if (s_settings.TryGetValue(key, out string? value)) {
            return value;
        }

        // If the key contains a period, it might be in the format "section.key".
        if (key.Contains('.')) {
            var parts = key.Split(['.'], 2);
            var section = parts[0];
            var sectionKey = parts[1];

            if (s_sections.TryGetValue(section, out var sectionSettings)
                && sectionSettings.TryGetValue(sectionKey, out value!)) {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets a section from the configuration
    /// </summary>
    /// <param name="sectionName">The section name</param>
    /// <returns>Dictionary containing all keys and values in the section</returns>
    public static Dictionary<string, string> GetSection(string sectionName) {
        EnsureInitialized();

        if (s_sections.TryGetValue(sectionName, out var section)) {
            return new Dictionary<string, string>(section);
        }

        return [];
    }

    /// <summary>
    /// Gets all section names from the configuration
    /// </summary>
    /// <returns>List of section names</returns>
    public static List<string> GetSectionNames() {
        EnsureInitialized();

        return [.. s_sections.Keys];
    }

    /// <summary>
    /// Reloads the configuration from the file
    /// </summary>
    public static void Reload() {
        EnsureInitialized();
        LoadConfiguration();
    }

    /// <summary>
    /// Gets a value as a specific type
    /// </summary>
    /// <typeparam name="T">The type to convert to</typeparam>
    /// <param name="key">The configuration key</param>
    /// <param name="defaultValue">The default value if key not found or conversion fails</param>
    /// <returns>The converted value or default value</returns>
    public static T GetValue<T>(string key, T defaultValue = default!) {
        string value = GetSetting(key) ?? string.Empty;

        if (string.IsNullOrEmpty(value)) {
            return defaultValue;
        }

        try {
            // Handle boolean values specifically for "True" and "False" strings.
            if (typeof(T) == typeof(bool)) {
                return (T) (object) bool.Parse(value);
            }

            // Handle Enum types.
            if (typeof(T).IsEnum) {
                return (T) Enum.Parse(typeof(T), value, true);
            }

            // Convert to the requested type.
            return (T) Convert.ChangeType(value, typeof(T));
        }
        catch {
            return defaultValue;
        }
    }

    private static void LoadConfiguration() {
        s_settings.Clear();
        s_sections.Clear();

        string? currentSection = null;
        Dictionary<string, string>? currentSectionSettings = null;

        foreach (string line in File.ReadAllLines(s_configFilePath)) {
            string trimmedLine = line.Trim();

            // Skip empty lines and comments.
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";")) {
                continue;
            }

            // Check if this is a section header.
            if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]")) {
                currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                currentSectionSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                s_sections[currentSection] = currentSectionSettings;

                continue;
            }

            // Parse key-value pair.
            int equalsIndex = trimmedLine.IndexOf('=');
            if (equalsIndex > 0) {
                string key = trimmedLine.Substring(0, equalsIndex).Trim();
                string value = trimmedLine.Substring(equalsIndex + 1).Trim();

                // Store in the appropriate collection.
                if (currentSection != null) {
                    if (currentSectionSettings != null) {
                        currentSectionSettings[key] = value;
                    }

                    // Also store as a fully qualified key for direct access.
                    s_settings[$"{currentSection}.{key}"] = value;
                }
                else {
                    s_settings[key] = value;
                }
            }
        }
    }

    private static void EnsureInitialized() {
        if (!s_isInitialized) {
            throw new InvalidOperationException("ConfigurationManager has not been initialized. Call Initialize() first.");
        }
    }

    /// <summary>
    /// Provides a fluent interface for accessing configuration settings with type conversion.
    /// </summary>
    public class ConfigSettings {

        /// <summary>
        /// Gets a configuration value by key
        /// </summary>
        /// <param name="key">The configuration key</param>
        /// <returns>A ConfigValue that can be converted to various types</returns>
        public ConfigValue this[string key] => new(GetSetting(key)!);

    }

    /// <summary>
    /// Represents a configuration value with flexible type conversion capabilities.
    /// </summary>
    public class ConfigValue {

        private readonly string _value;

        /// <summary>
        /// Initializes a new instance of the ConfigValue class
        /// </summary>
        /// <param name="value">The string value</param>
        public ConfigValue(string value)
            => _value = value;

        /// <summary>
        /// Implicitly converts a ConfigValue to a string
        /// </summary>
        /// <param name="configValue">The ConfigValue to convert</param>
        public static implicit operator string(ConfigValue configValue)
            => configValue._value;

        /// <summary>
        /// Converts the value to a byte
        /// </summary>
        /// <returns>The byte value</returns>
        public byte AsByte()
            => string.IsNullOrEmpty(_value) ? (byte) 0 : Convert.ToByte(_value);

        /// <summary>
        /// Converts the value to a byte with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The byte value or default</returns>
        public byte AsByte(byte defaultValue) {
            try {
                return AsByte();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a signed byte
        /// </summary>
        /// <returns>The signed byte value</returns>
        public sbyte AsSByte()
            => string.IsNullOrEmpty(_value) ? (sbyte) 0 : Convert.ToSByte(_value);

        /// <summary>
        /// Converts the value to a signed byte with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The signed byte value or default</returns>
        public sbyte AsSByte(sbyte defaultValue) {
            try {
                return AsSByte();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a short
        /// </summary>
        /// <returns>The short value</returns>
        public short AsShort()
            => string.IsNullOrEmpty(_value) ? (short) 0 : Convert.ToInt16(_value);

        /// <summary>
        /// Converts the value to a short with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The short value or default</returns>
        public short AsShort(short defaultValue) {
            try {
                return AsShort();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to an unsigned short
        /// </summary>
        /// <returns>The unsigned short value</returns>
        public ushort AsUShort()
            => string.IsNullOrEmpty(_value) ? (ushort) 0 : Convert.ToUInt16(_value);

        /// <summary>
        /// Converts the value to an unsigned short with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The unsigned short value or default</returns>
        public ushort AsUShort(ushort defaultValue) {
            try {
                return AsUShort();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to an integer
        /// </summary>
        /// <returns>The integer value</returns>
        public int AsInt()
            => string.IsNullOrEmpty(_value) ? 0 : Convert.ToInt32(_value);

        /// <summary>
        /// Converts the value to an integer with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The integer value or default</returns>
        public int AsInt(int defaultValue) {
            try {
                return AsInt();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to an unsigned integer
        /// </summary>
        /// <returns>The unsigned integer value</returns>
        public uint AsUInt()
            => string.IsNullOrEmpty(_value) ? 0u : Convert.ToUInt32(_value);

        /// <summary>
        /// Converts the value to an unsigned integer with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The unsigned integer value or default</returns>
        public uint AsUInt(uint defaultValue) {
            try {
                return AsUInt();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a long
        /// </summary>
        /// <returns>The long value</returns>
        public long AsLong()
            => string.IsNullOrEmpty(_value) ? 0L : Convert.ToInt64(_value);

        /// <summary>
        /// Converts the value to a long with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The long value or default</returns>
        public long AsLong(long defaultValue) {
            try {
                return AsLong();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to an unsigned long
        /// </summary>
        /// <returns>The unsigned long value</returns>
        public ulong AsULong()
            => string.IsNullOrEmpty(_value) ? 0UL : Convert.ToUInt64(_value);

        /// <summary>
        /// Converts the value to an unsigned long with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The unsigned long value or default</returns>
        public ulong AsULong(ulong defaultValue) {
            try {
                return AsULong();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a boolean
        /// </summary>
        /// <returns>The boolean value</returns>
        public bool AsBool() {
            if (string.IsNullOrEmpty(_value)) {
                return false;
            }

            return _value.ToLower() switch {
                "true" or "yes" or "1" or "on" or "enabled" => true,
                _ => false
            };
        }

        /// <summary>
        /// Converts the value to a boolean with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The boolean value or default</returns>
        public bool AsBool(bool defaultValue) {
            try {
                return AsBool();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a string
        /// </summary>
        /// <returns>The string value</returns>
        public string AsString()
            => _value ?? string.Empty;

        /// <summary>
        /// Converts the value to a string with a default if null
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The string value or default</returns>
        public string AsString(string defaultValue)
            => string.IsNullOrEmpty(_value) ? defaultValue : _value;

        /// <summary>
        /// Converts the value to a float
        /// </summary>
        /// <returns>The float value</returns>
        public float AsFloat()
            => string.IsNullOrEmpty(_value) ? 0f : Convert.ToSingle(_value);

        /// <summary>
        /// Converts the value to a float with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The float value or default</returns>
        public float AsFloat(float defaultValue) {
            try {
                return AsFloat();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a double
        /// </summary>
        /// <returns>The double value</returns>
        public double AsDouble()
            => string.IsNullOrEmpty(_value) ? 0d : Convert.ToDouble(_value);

        /// <summary>
        /// Converts the value to a double with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The double value or default</returns>
        public double AsDouble(double defaultValue) {
            try {
                return AsDouble();
            }
            catch {
                return defaultValue;
            }
        }

        /// <summary>
        /// Converts the value to a decimal
        /// </summary>
        /// <returns>The decimal value</returns>
        public decimal AsDecimal()
            => string.IsNullOrEmpty(_value) ? 0m : Convert.ToDecimal(_value);

        /// <summary>
        /// Converts the value to a decimal with a default if conversion fails
        /// </summary>
        /// <param name="defaultValue">The default value</param>
        /// <returns>The decimal value or default</returns>
        public decimal AsDecimal(decimal defaultValue) {
            try {
                return AsDecimal();
            }
            catch {
                return defaultValue;
            }
        }

    }

}