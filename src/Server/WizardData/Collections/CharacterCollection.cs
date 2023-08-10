using Imlight.Server.Game.Models;
using Raven.Client.Documents;

namespace Imlight.Server.WizardData.Implementations;

public static class CharacterCollection
{
    private const string CollectionName = "Characters";
    private static readonly IDocumentStore Store;

    static CharacterCollection()
    {
        Store = DocumentStoreSingleton.Store;
    }

    /// <summary>
    /// Creates a character in the database. Persists immediately.
    /// </summary>
    /// <param name="character"></param>
    public static async void AddCharacter(Character character)
    {
        using var session = Store.OpenAsyncSession();
        await session.StoreAsync(character);
        var metadata = session.Advanced.GetMetadataFor(character);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        
        await session.SaveChangesAsync();
    }

    /// <summary>
    /// Updates a character in the database. Persists immediately.
    /// </summary>
    /// <param name="id"></param>
    public static async void DeleteCharacter(ulong id)
    {
        using var session = Store.OpenAsyncSession();
        session.Delete(id);

        await session.SaveChangesAsync();
    }
}