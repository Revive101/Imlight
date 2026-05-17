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
 *
 * ========================================================================
 * AKKA.NET CONFIGURATION SYSTEM
 * ========================================================================
 * 
 * PURPOSE:
 * Provides a centralized mechanism for loading Akka.NET configuration files
 * and creating properly configured actor systems.
 * 
 * USAGE EXAMPLE:
 * if (AkkaConfiguration.CreateActorSystem("Imlight", out var actorSystem)) {
 *     // Use the actorSystem
 * }
 * 
 * NOTE:
 * This system uses System.Reflection and System.IO for file operations.
 * Configuration file must exist at Config/akka.conf relative to the assembly location.
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.IO;
using System.Reflection;
using Akka.Actor;
using Akka.Configuration;
using Imlight.Common;

namespace Imlight.Director;

/// <summary>
/// Handles the configuration and creation of Akka.NET actor systems.
/// </summary>
internal static class AkkaConfiguration {

    private const string CONFIGURATION_FILE_NAME = "akka.conf";

    private static readonly string ConfigLocation = Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
        $@"Config/{CONFIGURATION_FILE_NAME}");

    internal static bool CreateActorSystem(string name, out ActorSystem system) {
        system = null;

        if (!GetAkkaConfiguration(out var config)) {
            return false;
        }

        system = ActorSystem.Create(name, config);

        return true;
    }

    private static bool GetAkkaConfiguration(out Config config) {
        Logger.Information("Searching for Akka.NET configuration file {ConfigLocation}", Logger.Args(ConfigLocation));
        config = default;

        try {
            if (!File.Exists(ConfigLocation)) {
                return false;
            }

            var configContents = File.ReadAllText(ConfigLocation);

            config = ConfigurationFactory.ParseString(configContents);

            return true;
        }
        catch (Exception e) {
            Logger.Error(e.Message);

            return false;
        }
    }

}
