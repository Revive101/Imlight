using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Imlight.Common;

/// <summary>
/// Manages reading and persisting key-value environment variables in a .env file
/// </summary>
public static class EnvironmentManager {

    private static readonly Lock s_fileLock = new();
    private static bool s_isInitialized = false;
    private static string s_envFilePath = string.Empty;

    private static readonly string s_defaultContent = @"""
            # Imlight Environment Secrets (auto-generated)
            
            # This file is used to store environment variables for Imlight.
            # It should never be shared publicly.

            # Auto-generated if left blank.
            API_JWT_SECRET=

            # Optional Master/Bootstrap API token with full scopes for initial setup or recovery.
            API_MASTER_KEY=
            """;

    /// <summary>
    /// Provides indexer access to environment settings with the same ConfigValue conversions as ConfigurationManager.
    /// </summary>
    public static EnvSettings Settings { get; } = new();

    public static void Initialize(string envFilePath) {
        if (s_isInitialized) {
            throw new InvalidOperationException("EnvironmentManager is already initialized.");
        }

        lock (s_fileLock) {
            if (!File.Exists(envFilePath)) {
                File.WriteAllText(envFilePath, s_defaultContent + Environment.NewLine);
            }

            s_envFilePath = envFilePath;
            LoadFromFile();
            s_isInitialized = true;
        }
    }

    /// <summary>
    /// Gets an environment variable from System.Environment (auto-initializing from .env if needed).
    /// </summary>
    public static string? Get(string key) {
        EnsureInitialized();
        return Environment.GetEnvironmentVariable(key);
    }

    /// <summary>
    /// Gets an environment variable, or generates and saves it to .env if it is missing or empty.
    /// </summary>
    public static string GetOrCreate(string key, Func<string> valueFactory, string? comment = null) {
        var existing = Get(key);

        if (!string.IsNullOrWhiteSpace(existing)) {
            return existing;
        }

        var generated = valueFactory();
        Set(key, generated, comment);

        return generated;
    }

    /// <summary>
    /// Sets an environment variable in System.Environment and persists it to the .env file on disk.
    /// </summary>
    public static void Set(string key, string value, string? comment = null) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("Environment key cannot be empty.", nameof(key));
        }
        EnsureInitialized();
        key = key.Trim();
        value ??= string.Empty;
        // 1. Update process environment in memory immediately
        Environment.SetEnvironmentVariable(key, value);
        // 2. Persist to .env file so it survives restarts
        lock (s_fileLock) {
            PersistKeyToFile(key, value, comment);
        }
    }

    private static void LoadFromFile() {
        foreach (var rawLine in File.ReadAllLines(s_envFilePath)) {
            if (TryParseKeyValue(rawLine, out var key, out var value)) {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static void PersistKeyToFile(string targetKey, string newValue, string? comment) {
        var lines = File.Exists(s_envFilePath)
            ? new List<string>(File.ReadAllLines(s_envFilePath))
            : [];

        var formattedValue = FormatEnvValue(newValue);
        var updated = false;

        // If the key already exists in .env, replace just that line in-place
        for (var i = 0; i < lines.Count; i++) {
            if (TryParseKeyValue(lines[i], out var existingKey, out _) &&
                string.Equals(existingKey, targetKey, StringComparison.OrdinalIgnoreCase)) {
                lines[i] = $"{targetKey}={formattedValue}";
                updated = true;
                break;
            }
        }

        // Otherwise append it at the end of the file
        if (!updated) {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1])) {
                lines.Add(string.Empty);
            }
            if (!string.IsNullOrWhiteSpace(comment)) {
                lines.Add($"# {comment}");
            }
            lines.Add($"{targetKey}={formattedValue}");
        }

        // Write to a temp file and atomically replace so .env is never corrupted mid-write
        var tempPath = s_envFilePath + ".tmp";
        File.WriteAllLines(tempPath, lines);
        File.Move(tempPath, s_envFilePath, overwrite: true);
    }

    private static bool TryParseKeyValue(string line, out string key, out string value) {
        key = string.Empty;
        value = string.Empty;
        var trimmed = line.Trim();

        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith(';')) {
            return false;
        }

        if (trimmed.StartsWith("export ", StringComparison.OrdinalIgnoreCase)) {
            trimmed = trimmed["export ".Length..].TrimStart();
        }

        var eqIndex = trimmed.IndexOf('=');
        if (eqIndex <= 0) {
            return false;
        }

        key = trimmed[..eqIndex].Trim();
        var rawVal = trimmed[(eqIndex + 1)..].Trim();

        // Strip surrounding quotes if present ("..." or '...')
        if (rawVal.Length >= 2 &&
            ((rawVal.StartsWith('"') && rawVal.EndsWith('"')) ||
             (rawVal.StartsWith('\'') && rawVal.EndsWith('\'')))) {
            rawVal = rawVal[1..^1];
        }

        value = rawVal;
        return !string.IsNullOrEmpty(key);
    }

    private static string FormatEnvValue(string value) {
        if (value.Contains(' ') || value.Contains('#') || value.Contains(';')) {
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        return value;
    }

    private static void EnsureInitialized() {
        if (!s_isInitialized) {
            Initialize(".env");
        }
    }

    public sealed class EnvSettings {
        public ConfigurationManager.ConfigValue this[string key] {
            get => new(Get(key) ?? string.Empty);
            set => Set(key, value.AsString());
        }
    }
}
