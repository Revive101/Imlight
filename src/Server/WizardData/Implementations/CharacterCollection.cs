using System.Linq;
using System.Threading.Tasks;
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
    /// Creates a character in the database.
    /// </summary>
    /// <param name="character"></param>
    public static bool AddCharacter(Character character)
    {
        using var session = Store.OpenSession();
        
        // Return false if the character already exists in the database.
        var existingCharacter = session.Query<Character>()
            .FirstOrDefault(x => x.CharId == character.CharId);
        if (existingCharacter is not null)
            return false;
        
        session.Store(character);
        var metadata = session.Advanced.GetMetadataFor(character);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        
        session.SaveChanges();
        return true;
    }

    /// <summary>
    /// Updates a character in the database.
    /// </summary>
    /// <param name="id"></param>
    public static bool DeleteCharacter(ulong id)
    {
        using var session = Store.OpenSession();
        
        var character = session.Query<Character>()
            .FirstOrDefault(x => x.CharId == id);
        if (character is null)
            return false;

        session.Delete(character);
        session.SaveChanges();
        return true;
    }
}