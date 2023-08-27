using System;
using System.Collections.Generic;
using Imlight.Common.Configuration;
using Imlight.Common.Utilities;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Embedded;

namespace Imlight.Server.WizardData.Implementations;

public static class EmbeddedDatabaseManager
{
    public static bool IsRunning { get; private set; }
    
    public static void Start()
    {
        try
        {
            if (IsRunning)
                return;

            Log.Information("Initializing embedded RavenDB database for the first time..");

            // Configure the embedded server.
            var serverOptions = new ServerOptions
            {
                DataDirectory = ConfigurationManager.Settings.EmbeddedDatabaseDataDirectory,
                ServerUrl = $"http://127.0.0.1:{ConfigurationManager.Settings.EmbeddedDatabasePort}",
                MaxServerStartupTimeDuration =
                    TimeSpan.FromSeconds(ConfigurationManager.Settings.EmbeddedDatabaseTimeoutTime),
                CommandLineArgs = new List<string> { "--Databases.MaxIdleTimeInSec=-1" }
            };

            // If we're using the full database, we need to set the server directory.
            // Otherwise, we're using the embedded database.
            var useFullDb = ConfigurationManager.Settings.EmbeddedDatabaseUseFull;
            if (useFullDb)
                serverOptions.ServerDirectory = ConfigurationManager.Settings.EmbeddedDatabaseFullPath;

            EmbeddedServer.Instance.StartServer(serverOptions);
            IsRunning = true;
            
            EmbeddedServer.Instance.ServerProcessExited += (sender, args) =>
            {
                Log.Error("Embedded database process exited unexpectedly. Restarting..");
                EmbeddedServer.Instance.RestartServerAsync().Wait();
                Log.Information("Embedded database restarted.");
            };

            Log.Information("Embedded database initialized.");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to initialize embedded database: {0}", Log.Args(ex.Message));
        }
    }

    public static IDocumentStore GetDocumentStore(string databaseName)
    {
        if (!IsRunning)
            return null;
        
        var databaseOptions = new DatabaseOptions(new DatabaseRecord { DatabaseName = databaseName });
        var docStore = EmbeddedServer.Instance.GetDocumentStore(databaseOptions);
        return docStore;
    }
}