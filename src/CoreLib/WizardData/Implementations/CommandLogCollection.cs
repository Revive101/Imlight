using Imlight.CoreLib.WizardData.Models;
using Raven.Client.Documents;
using System;

namespace Imlight.CoreLib.WizardData.Implementations;

public static class CommandLogCollection {
    public const string CollectionName = "CommandLog";
    private static readonly IDocumentStore s_store;

    static CommandLogCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    public static void AddCommandLog(ChatLog chatLog) {
        using var session = s_store.OpenSession();

        session.Store(chatLog);
        var metadata = session.Advanced.GetMetadataFor(chatLog);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }
}
