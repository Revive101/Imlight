/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
