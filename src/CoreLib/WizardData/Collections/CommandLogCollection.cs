/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Player;
using Raven.Client.Documents;

namespace Imlight.CoreLib.WizardData.Collections;

public static class CommandLogCollection {
    public const string CollectionName = "CommandLog";
    private static readonly IDocumentStore s_store;

    static CommandLogCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds a command log to the collection.
    /// </summary>
    /// <param name="chatLog">The chat log to add.</param>
    public static void AddCommandLog(ChatLog chatLog) {
        using var session = s_store.OpenSession();

        session.Store(chatLog);
        var metadata = session.Advanced.GetMetadataFor(chatLog);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }
}
