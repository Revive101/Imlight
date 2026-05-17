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
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Embedded;
using Imlight.Common;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class EmbeddedDatabaseManager {

    public static bool IsRunning { get; private set; }

    private static readonly string s_embeddedDatabasePath 
        = ConfigurationManager.Settings["Database.EmbeddedDatabaseDataDirectory"];
    private static readonly bool s_embeddedDatabaseUseFull 
        = ConfigurationManager.Settings["Database.EmbeddedDatabaseUseFull"].AsBool();
    private static readonly string s_embeddedDatabaseFullPath 
        = ConfigurationManager.Settings["Database.EmbeddedDatabaseFullPath"];
    private static readonly ushort s_embeddedDatabasePort 
        = ConfigurationManager.Settings["Database.EmbeddedDatabasePort"].AsUShort();
    private static readonly long s_embeddedDatabaseTimeoutTime 
        = ConfigurationManager.Settings["Database.EmbeddedDatabaseTimeoutTime"].AsLong();

    public static void Start() {
        try {
            if (IsRunning) {
                return;
            }

            Logger.Information("Initializing embedded RavenDB database for the first time..");
            Logger.Information("Database data directory: {0}", Logger.Args(s_embeddedDatabasePath));

            // Configure the embedded server.
            var serverOptions = new ServerOptions {
                DataDirectory = s_embeddedDatabasePath,
                ServerUrl = $"http://127.0.0.1:{s_embeddedDatabasePort}",
                MaxServerStartupTimeDuration = TimeSpan.FromSeconds(s_embeddedDatabaseTimeoutTime),
                CommandLineArgs = ["--Databases.MaxIdleTimeInSec=-1"]
            };

            // If we're using the full database, we need to set the server directory.
            // Otherwise, we're using the embedded database.
            if (s_embeddedDatabaseUseFull) {
                serverOptions.ServerDirectory = s_embeddedDatabaseFullPath;

                Logger.Information("Using full RavenDB server at {0}", 
                    Logger.Args(s_embeddedDatabaseFullPath));
            }

            EmbeddedServer.Instance.StartServer(serverOptions);
            IsRunning = true;

            EmbeddedServer.Instance.ServerProcessExited += (sender, args) => {
                Logger.Error("Embedded database process exited unexpectedly. Restarting..");
                
                try {
                    EmbeddedServer.Instance.RestartServerAsync().Wait();
                }
                catch (Exception ex) {
                    Logger.Error("Failed to restart embedded database: {0}", Logger.Args(ex.Message));
                }

                Logger.Information("Embedded database restarted.");
            };

            Logger.Information("Embedded database initialized.");
        }
        catch (Exception ex) {
            Logger.Error("Failed to initialize embedded database: {0}", Logger.Args(ex.Message));
        }
    }

    public static IDocumentStore GetDocumentStore(string databaseName) {
        if (!IsRunning) {
            return null;
        }

        var databaseOptions = new DatabaseOptions(new DatabaseRecord { DatabaseName = databaseName });
        var docStore = EmbeddedServer.Instance.GetDocumentStore(databaseOptions);
        
        return docStore;
    }

}
