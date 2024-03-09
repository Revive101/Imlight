/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Security.Cryptography.X509Certificates;
using Serilog;
using Raven.Client.Documents;
using Raven.Client.ServerWide;
using Raven.Embedded;
using DragonNPCTool.Models;
using static System.Console;

namespace DragonNPCTool.Managers;

public static class DragonDatabaseManager {
    private const ushort EmbeddedDatabasePort = 8080;

    private static readonly string _databaseName = "WorldData";
    private static readonly string _collectionName = "NpcInventory";

    private static IDocumentStore? _store;
    public static IDocumentStore? Store => _store ??= _isInEmbeddedMode ? CreateEmbeddedStore() : CreateRemoteStore();

    private static bool _isInEmbeddedMode;
    private static string _embeddedDataPath = "EmbeddedData";
    private static string _remoteUrl;
    private static string _certificateUrl;

    public static void SetEmbeddedServer(string dataPath) {
        _isInEmbeddedMode = true;
        _embeddedDataPath = dataPath;

        var t = _store = CreateEmbeddedStore();
    }

    public static void SetRemoteServer(string url, string certificateUrl) {
        _isInEmbeddedMode = false;
        _remoteUrl = url;
        _certificateUrl = certificateUrl;
    }

    private static IDocumentStore? CreateEmbeddedStore() {
        if (_embeddedDataPath is null or "") {
            throw new Exception("Embedded data path is null or empty.");
        }

        WriteLine("Initializing embedded RavenDB database for the first time..");

        // Configure the embedded server.
        var serverOptions = new ServerOptions {
            DataDirectory = _embeddedDataPath,
            ServerUrl = $"http://127.0.0.1:{EmbeddedDatabasePort}",
            CommandLineArgs = new List<string> { "--Databases.MaxIdleTimeInSec=-1" }
        };

        EmbeddedServer.Instance.StartServer(serverOptions);
        EmbeddedServer.Instance.ServerProcessExited += (sender, args) => {
            Log.Error("Embedded database process exited unexpectedly. Restarting..");
            EmbeddedServer.Instance.RestartServerAsync().Wait();
            WriteLine("Embedded database restarted");
        };

        WriteLine("Embedded database initialized on port {0}", EmbeddedDatabasePort);

        var databaseOptions = new DatabaseOptions(new DatabaseRecord(databaseName: _databaseName));
        return EmbeddedServer.Instance.GetDocumentStore(databaseOptions);
    }
    private static IDocumentStore? CreateRemoteStore() {
        WriteLine("Initializing remote RavenDB database for the first time..");
        WriteLine("Database name: {0}", _databaseName);

        // Try to make an x509 certificate from the certificate file.
        if (!File.Exists(_certificateUrl)) {
            throw new Exception("Certificate file does not exist.");
        }

        var certificate = new X509Certificate2(_certificateUrl);

        var store = new DocumentStore {
            Urls = new[] { _remoteUrl },
            Database = _databaseName,
            Conventions =
            {
                MaxNumberOfRequestsPerSession = 16,
                UseOptimisticConcurrency = true,
                RequestTimeout = TimeSpan.FromSeconds(90),
                WaitForNonStaleResultsTimeout = TimeSpan.FromSeconds(90),
            },
            Certificate = certificate
        }.Initialize();

        WriteLine("Database initialized");

        return store;
    }

    public static void AddNpcInventory(NPCInventory npcInventory) {
        using var session = Store!.OpenSession();

        session.Store(npcInventory);
        var metadata = session.Advanced.GetMetadataFor(npcInventory);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = _collectionName;

        session.SaveChanges();
    }

    public static bool UpdateNpcInventory(NPCInventory npcInventory) {
        using var session = Store!.OpenSession();

        // Check if the NPCInventory already exists
        var existingNpcInventory = session.Query<NPCInventory>(collectionName: _collectionName)
            .Where(x => x.TemplateID == npcInventory.TemplateID)
            .FirstOrDefault();

        if (existingNpcInventory != null) {
            existingNpcInventory.Inventory = npcInventory.Inventory;
        }
        else {
            return false;
        }

        session.SaveChanges();
        return true;
    }

    public static bool TryGetNpcInventory(ulong templateID, out NPCInventory ?npcInventory) {
        using var session = Store!.OpenSession();

        npcInventory = session.Query<NPCInventory>(collectionName: _collectionName)
            .Where(x => x.TemplateID == templateID)
            .FirstOrDefault();

        return npcInventory != null;
    }
}
