/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
*/

using System.Linq;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.WizardData.Implementations;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.WizardData.Collections;

public static class AccountCollection {
    
    public const string CollectionName = "Accounts";
    private static readonly IDocumentStore s_store;

    static AccountCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Creates a new account in the database.
    /// </summary>
    /// <param name="account">The account to be created.</param>
    /// <returns>True if the account is successfully created, false if the account already exists.</returns>
    public static bool CreateAccount(Account account) {
        using var session = s_store.OpenSession();

        // Return false if the account already exists.
        if (session.Query<Account>(collectionName: CollectionName)
                   .Any(c => c.Username == account.Username)) {
            return false;
        }

        // Foreach character in the account, add it to the database.
        foreach (var character in account.Characters) {
            WizardCollection.AddCharacter(character);
        }

        session.Store(account);
        var metadata = session.Advanced.GetMetadataFor(account);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
        
        return true;
    }

    /// <summary>
    /// Deletes an account and all associated characters from the database.
    /// </summary>
    /// <param name="username">The username of the account to delete.</param>
    /// <returns>True if the account was successfully deleted, false otherwise.</returns>
    public static bool DeleteAccount(string username) {
        using var session = s_store.OpenSession();

        // Load the account with the characters included.
        var account = session.Query<Account>(collectionName: CollectionName)
            .Include(c => c.CharacterIds)
            .FirstOrDefault(c => c.Username == username);
        if (account is null) {
            return false;
        }

        // Delete the characters.
        foreach (var characterId in account.CharacterIds) {
            WizardCollection.DeleteCharacter(characterId);
        }

        // Delete the account.
        session.Delete(account);
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Gets an account from the database by its ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static Account GetAccount(ulong id) {
        using var session = s_store.OpenSession();

        // Load the account with the characters included.
        var account = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(c => c.AccountId == id);
        if (account is null) {
            return null;
        }

        // Load the characters.
        var _ = WizardCollection.LoadWizardsOntoAccount(account.AccountId, ref account);

        // Load infractions. The constructor will load the action history.
        var infractions = session.Query<Infraction>(collectionName: InfractionCollection.CollectionName)
            .Where(i => i.AccountId == account.AccountId)
            .ToList();

        // Rebuild the InfractionHistory object.
        account.InfractionHistory = new InfractionHistory(account.AccountId, infractions);

        return account;
    }

    /// <summary>
    /// Gets an account from the database by its username.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public static Account GetAccount(string username) {
        using var session = s_store.OpenSession();

        // Load the account with the characters included.
        var account = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(c => c.Username == username);
        if (account is null) {
            return null;
        }

        // Load the characters if the account is not null.
        var _ = WizardCollection.LoadWizardsOntoAccount(account.AccountId, ref account);

        // Load infractions. The constructor will load the action history.
        var infractions = session.Query<Infraction>(collectionName: InfractionCollection.CollectionName)
            .Where(i => i.AccountId == account.AccountId)
            .ToList();

        // Rebuild the InfractionHistory object.
        account.InfractionHistory = new InfractionHistory(account.AccountId, infractions);

        return account;
    }

    /// <summary>
    /// Locks the specified account by setting its IsLocked property to true.
    /// </summary>
    /// <param name="account">The account to be locked.</param>
    /// <returns>True if the account was successfully locked, false otherwise.</returns>
    public static bool LockAccount(string username) {
        using var session = s_store.OpenSession();

        // Load the account with the characters included.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .Include(c => c.CharacterIds)
            .FirstOrDefault(c => c.Username == username);
        if (existingAccount is null) {
            return false;
        }

        existingAccount.IsLocked = true;
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Unlocks the account with the specified username.
    /// </summary>
    /// <param name="username">The username of the account to unlock.</param>
    /// <returns>True if the account was successfully unlocked, false otherwise.</returns>
    public static bool UnlockAccount(string username) {
        using var session = s_store.OpenSession();

        // Load the account with the characters included.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .Include(c => c.CharacterIds)
            .FirstOrDefault(c => c.Username == username);
        if (existingAccount is null) {
            return false;
        }

        existingAccount.IsLocked = false;
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Changes the password for the specified account.
    /// </summary>
    /// <param name="account">The account to change the password for.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>True if the password was successfully changed, false otherwise.</returns>
    public static bool ChangePassword(string username, string newPassword) {
        using var session = s_store.OpenSession();

        // Load the account with the characters included.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .Include(c => c.CharacterIds)
            .FirstOrDefault(c => c.Username == username);
        if (existingAccount is null) {
            return false;
        }

        var passwordHash = DatabaseUtilities.CreateHashedPassword(newPassword);
        existingAccount.PasswordHash = passwordHash;
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Updates the authentication level of an account.
    /// </summary>
    /// <param name="account">The account to update.</param>
    /// <param name="authLevel">The new authentication level.</param>
    /// <returns>True if the update was successful, false otherwise.</returns>
    public static bool UpdateAuthLevel(string username, AuthLevel authLevel) {
        using var session = s_store.OpenSession();

        // Load the account with the characters included.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .Include(c => c.CharacterIds)
            .FirstOrDefault(c => c.Username == username);
        if (existingAccount is null) {
            return false;
        }

        existingAccount.AuthLevel = authLevel;
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Adds a character to an account.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="characterId"></param>
    /// <returns></returns>
    public static bool AddCharacterToAccount(ulong accountId, ulong characterId) {
        using var session = s_store.OpenSession();

        // Start by loading an account, if one exists.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(c => c.AccountId == accountId);
        if (existingAccount is null) {
            return false;
        }

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
    public static bool DeleteCharacterFromAccount(ulong accountId, ulong characterId) {
        using var session = s_store.OpenSession();

        // Start by loading an account, if one exists.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(c => c.AccountId == accountId);
        if (existingAccount is null) {
            return false;
        }

        existingAccount.CharacterIds.Remove(characterId);
        session.SaveChanges();

        return true;
    }

    /// <summary>
    /// Adds an infraction to the account with the specified account ID.
    /// </summary>
    /// <param name="accountId">The ID of the account to add the infraction to.</param>
    /// <param name="infractionId">The ID of the infraction to add.</param>
    public static void AddInfractionToAccount(ulong accountId, ulong infractionId) {
        using var session = s_store.OpenSession();

        // Start by loading an account, if one exists.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(c => c.AccountId == accountId);
        if (existingAccount is null) {
            return;
        }

        existingAccount.InfractionIds.Add(infractionId);
        session.SaveChanges();
    }

    /// <summary>
    /// Removes an infraction from an account.
    /// </summary>
    /// <param name="accountId">The ID of the account.</param>
    /// <param name="infractionIndex">The index of the infraction to remove.</param>
    public static void RemoveInfractionFromAccount(ulong accountId, ulong infractionIndex) {
        using var session = s_store.OpenSession();

        // Start by loading an account, if one exists.
        var existingAccount = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(c => c.AccountId == accountId);
        if (existingAccount is null) {
            return;
        }

        existingAccount.InfractionIds.Remove(infractionIndex);
        session.SaveChanges();
    }

}
