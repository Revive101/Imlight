using System;
using System.Linq;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;
using WizUnraveler.IO;

namespace Imlight.Server.WizardData.Implementations;

public class ClientKeyPair
{
    public ulong AccountId { get; set; }
    public ulong MachineId { get; set; }
    public string ClientKey2 { get; set; }

    public ClientKeyPair(ulong accountId, ulong machineId, string clientKey2)
    {
        AccountId = accountId;
        MachineId = machineId;
        ClientKey2 = clientKey2 ?? throw new ArgumentNullException(nameof(clientKey2));
    }
}

public static class ClientKeyCollection
{
    private const string CollectionName = "SessionKeys";
    
    private static readonly IDocumentStore Store;
    private const uint KeyExpireTimeInDays = 3;

    static ClientKeyCollection()
    {
        Store = DocumentStoreSingleton.Store;
    }

    public static void AddSessionKey(ulong accountId, ulong machineId, string key)
    {
        using var session = Store.OpenSession();

        // Remove any existing document that matches the account id.
        Store
            .Operations
            .Send(new DeleteByQueryOperation(new IndexQuery()
            {
                Query = $"from {CollectionName} where AccountId = '{accountId}'"
            }));

        // Store a new ClientKeyPair in the database with an expiry date.
        var pair = new ClientKeyPair(accountId, machineId, key);
        var expiry = DateTime.UtcNow.AddDays(KeyExpireTimeInDays);

        // Store and set the metadata of the new document.
        session.Store(pair);
        var metadata = session.Advanced.GetMetadataFor(pair);
        metadata[Constants.Documents.Metadata.Collection] = CollectionName;
        metadata[Constants.Documents.Metadata.Expires] = expiry;

        session.SaveChanges();
    }

    public static ByteString GetSessionKey(ulong accountId, ulong machineId)
    {
        using var session = Store.OpenSession();

        // Get the ClientKeyPair from the database where the account id and machine id match.
        var pair = session.Query<ClientKeyPair>(collectionName: CollectionName)
            .FirstOrDefault(x => x.AccountId == accountId && x.MachineId == machineId);

        return pair?.ClientKey2;
    }
}