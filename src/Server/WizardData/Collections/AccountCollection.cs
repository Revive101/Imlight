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
        Store = DocumentStoreSingleton.Store;
    }

    /// <summary>
    /// Creates an account in the database. Persists immediately.
    /// </summary>
    /// <param name="account">The created account.</param>
    public static async void CreateAccount(Account account)
    {
        using var session = Store.OpenAsyncSession();
        await session.StoreAsync(account);
        var metadata = session.Advanced.GetMetadataFor(account);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        
        await session.SaveChangesAsync();
    }
    
    /// <summary>
    /// Updates an account in the database. Persists immediately. Returns false if the account does not exist.
    /// </summary>
    /// <param name="account"></param>
    /// <returns></returns>
    public static async Task<bool> UpdateAccount(Account account)
    {
        using var session = Store.OpenAsyncSession();
        
        // Start by loading an account, if one exists.
        var existingAccount = session.Query<Account>()
            .First(c => c.AccountId == account.AccountId);
        if (existingAccount is null)
            return false;

        await session.StoreAsync(account);
        var metadata = session.Advanced.GetMetadataFor(account);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        
        await session.SaveChangesAsync();
        return true;
    }
    
    /// <summary>
    /// Deletes an account from the database. Persists immediately.
    /// </summary>
    /// <param name="account"></param>
    public static async void DeleteAccount(Account account)
    {
        using var session = Store.OpenAsyncSession();
        session.Delete(account);
        await session.SaveChangesAsync();
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
        var account = session.Query<Account>()
            .Include(c => c.CharacterIds)
            .First(c => c.AccountId == id);
        
        // Load the characters if the account is not null.
        if (account is not null)
        {
            var characters = session.Query<Character>()
                .Where(c => c.AccountId == id)
                .ToList();
            account.Characters = characters;
        }

        return account;
    }
    
    public static Account GetAccount(string username)
    {
        using var session = Store.OpenSession();

        // Load the account with the characters included.
        var account = session.Query<Account>()
            .Include(c => c.CharacterIds)
            .First(c => c.Username == username);
        
        // Load the characters if the account is not null.
        if (account is not null)
        {
            var characters = session.Query<Character>()
                .Where(c => c.AccountId == account.AccountId)
                .ToList();
            account.Characters = characters;
        }

        return account;
    }
}