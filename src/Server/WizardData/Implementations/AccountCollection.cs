using System.Linq;
using System.Threading.Tasks;
using Imlight.Server.Game.Models;
using Imlight.Server.Login.Models;
using Raven.Client.Documents;

namespace Imlight.Server.WizardData.Implementations;

public static class AccountCollection
{
    private const string CollectionName = "Accounts";
    private static readonly IDocumentStore Store;

    static AccountCollection()
    {
        Store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Creates an account in the database. 
    /// </summary>
    /// <param name="account">The created account.</param>
    public static void CreateAccount(Account account)
    {
        using var session = Store.OpenSession();
        
        // Foreach character in the account, add it to the database.
        foreach (var character in account.Characters)
            CharacterCollection.AddCharacter(character);
        
        session.Store(account);
        var metadata = session.Advanced.GetMetadataFor(account);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        
        session.SaveChanges();
    }

    /// <summary>
    /// Deletes an account from the database.
    /// </summary>
    /// <param name="account"></param>
    public static void DeleteAccount(Account account)
    {
        using var session = Store.OpenAsyncSession();
        session.Delete(account);
        session.SaveChangesAsync();
    }

    /// <summary>
    /// Gets an account from the database by its ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static Account GetAccount(ulong id)
    {
        using var session = Store.OpenSession();

        // Load the account with the characters included.
        var account = session.Query<Account>(collectionName: CollectionName)
            .Include(c => c.CharacterIds)
            .FirstOrDefault(c => c.AccountId == id);
        if (account is null)
            return null;
        
        // Load the characters if the account is not null.
        var characters = session.Query<Character>()
            .Where(c => c.AccountId == id)
            .ToList();
        account.Characters = characters;

        return account;
    }
    
    /// <summary>
    /// Gets an account from the database by its username.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public static Account GetAccount(string username)
    {
        using var session = Store.OpenSession();

        // Load the account with the characters included.
        var account = session.Query<Account>(collectionName: CollectionName)
            .Include(c => c.CharacterIds)
            .FirstOrDefault(c => c.Username == username);
        if (account is null) 
            return null;
        
        // Load the characters if the account is not null.
        var characters = session.Query<Character>()
            .Where(c => c.AccountId == account.AccountId)
            .ToList();
        account.Characters = characters;

        return account;
    }
    
    /// <summary>
    /// Adds a character to an account.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="characterId"></param>
    /// <returns></returns>
    public static bool AddCharacterToAccount(ulong accountId, ulong characterId)
    {
        using var session = Store.OpenSession();
        
        // Start by loading an account, if one exists.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(c => c.AccountId == accountId);
        if (existingAccount is null)
            return false;

        existingAccount.CharacterIds.Add(characterId);
        session.SaveChanges();
        
        return true;
    }
    
    /// <summary>
    /// Removes a character from an account.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="characterId"></param>
    /// <returns></returns>
    public static bool DeleteCharacterFromAccount(ulong accountId, ulong characterId)
    {
        using var session = Store.OpenSession();
        
        // Start by loading an account, if one exists.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(c => c.AccountId == accountId);
        if (existingAccount is null)
            return false;

        existingAccount.CharacterIds.Remove(characterId);
        session.SaveChanges();
        
        return true;
    }
}