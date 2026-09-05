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
using Raven.Client.Documents.Session;
using System;

namespace Imlight.CoreLib.WizardData.Collections;

public static class AccountCollection {
    
    public const string CollectionName = "Accounts";
    private static readonly IDocumentStore s_store;

    static AccountCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    private const int WriteLaneCount = 1 << 6; // 64 lanes
    private const ulong WriteLaneMask = WriteLaneCount - 1;
    private static readonly object[] s_writeLanes =
        Enumerable.Range(0, WriteLaneCount)
            .Select(_ => new object())
            .ToArray();

    // Tracks the write lane held by the current thread.
    // Enforces that an account lock is acquired before a character lock
    // and prevents acquiring multiple Account lanes at the same time.
    [ThreadStatic]
    private static int? s_heldWriteLane;

    private static T WithWriteLane<T>(ulong accountId, Func<T> write) {
        if (WizardCollection.HoldsWriteLane)
            throw new InvalidOperationException("Cannot acquire an account write lane while holding a wizard write lane.");

        var laneIndex = (int) (accountId & WriteLaneMask);
        if (s_heldWriteLane is { } heldLane && heldLane != laneIndex)
            throw new InvalidOperationException($"Cannot acquire account lane {laneIndex} while holding account lane {heldLane}.");

        var writeLane = s_writeLanes[laneIndex];

        lock (writeLane) {
            var previousLane = s_heldWriteLane;
            s_heldWriteLane = laneIndex;

            try {
                return write();
            }
            finally {
                s_heldWriteLane = previousLane;
            }
        }
    }

    private static bool UpdateAccount(ulong accountId, Action<Account> update) {
        return WithWriteLane(accountId, () => {
            using var session = s_store.OpenSession();

            var existingAccount = session.Query<Account>(collectionName: CollectionName)
                .FirstOrDefault(account => account.AccountId == accountId);

            if (existingAccount is null)
                return false;

            update(existingAccount);

            session.SaveChanges();

            return true;
        });
    }

    private static ulong? GetAccountId(string username) {
        using var session = s_store.OpenSession();

        return session.Query<Account>(collectionName: CollectionName)
            .Where(account => account.Username == username)
            .Select(account => (ulong?) account.AccountId)
            .FirstOrDefault();
    }

    private static bool UpdateAccount(string username, Action<Account> update) {
        var accountId = GetAccountId(username);

        if (accountId is null)
            return false;

        return UpdateAccount(accountId.Value, update);
    }

    private static Account LoadAccountDetails(IDocumentSession session, Account account) {
        WizardCollection.LoadWizardsOntoAccount(account.AccountId, ref account);

        var infractions = session.Query<Infraction>(collectionName: InfractionCollection.CollectionName)
            .Where(i => i.AccountId == account.AccountId)
            .ToList();

        account.InfractionHistory = new InfractionHistory(account.AccountId, infractions);

        return account;
    }

    /// <summary>
    /// Creates a new account in the database.
    /// </summary>
    /// <param name="account">The account to be created.</param>
    /// <returns>True if the account is successfully created, false if the account already exists.</returns>
    public static bool CreateAccount(Account account) {
        return WithWriteLane(account.AccountId, () => {
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
        });
    }

    /// <summary>
    /// Deletes an account and all associated characters from the database.
    /// </summary>
    /// <param name="username">The username of the account to delete.</param>
    /// <returns>True if the account was successfully deleted, false otherwise.</returns>
    public static bool DeleteAccount(string username) {
        var id = GetAccountId(username);

        if (id is null)
            return false;

        var accountId = id.Value;

        return WithWriteLane(accountId, () => {
            using var session = s_store.OpenSession();

            var account = session.Query<Account>(collectionName: CollectionName)
                .FirstOrDefault(a => a.AccountId == accountId);

            if (account is null)
                return false;

            // Delete the characters.
            foreach (var characterId in account.CharacterIds) {
                WizardCollection.DeleteCharacter(characterId);
            }

            // Delete the account.
            session.Delete(account);
            session.SaveChanges();

            return true;
        });
    }

    /// <summary>
    /// Gets an account from the database by its ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static Account GetAccount(ulong id) {
        using var session = s_store.OpenSession();

        var account = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(a => a.AccountId == id);

        return account is null
            ? null
            : LoadAccountDetails(session, account);
    }

    /// <summary>
    /// Gets an account from the database by its username.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public static Account GetAccount(string username) {
        using var session = s_store.OpenSession();

        var account = session.Query<Account>(collectionName: CollectionName)
            .FirstOrDefault(a => a.Username == username);

        return account is null
            ? null
            : LoadAccountDetails(session, account);
    }

    /// <summary>
    /// Locks the specified account by setting its IsLocked property to true.
    /// </summary>
    /// <param name="account">The account to be locked.</param>
    /// <returns>True if the account was successfully locked, false otherwise.</returns>
    public static bool LockAccount(string username) {
        return UpdateAccount(username, account => {
            account.IsLocked = true;
        });
    }

    /// <summary>
    /// Unlocks the account with the specified username.
    /// </summary>
    /// <param name="username">The username of the account to unlock.</param>
    /// <returns>True if the account was successfully unlocked, false otherwise.</returns>
    public static bool UnlockAccount(string username) {
        return UpdateAccount(username, account => {
            account.IsLocked = false;
        });
    }

    /// <summary>
    /// Changes the password for the specified account.
    /// </summary>
    /// <param name="account">The account to change the password for.</param>
    /// <param name="newPassword">The new password.</param>
    /// <returns>True if the password was successfully changed, false otherwise.</returns>
    public static bool ChangePassword(string username, string newPassword) {
        return UpdateAccount(username, account => {
            var passwordHash = DatabaseUtilities.CreateHashedPassword(newPassword);
            account.PasswordHash = passwordHash;
        });
    }

    /// <summary>
    /// Updates the authentication level of an account.
    /// </summary>
    /// <param name="account">The account to update.</param>
    /// <param name="authLevel">The new authentication level.</param>
    /// <returns>True if the update was successful, false otherwise.</returns>
    public static bool UpdateAuthLevel(string username, AuthLevel authLevel) {
        return UpdateAccount(username, account => {
            account.AuthLevel = authLevel;
        });
    }

    /// <summary>
    /// Adds a character to an account.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="characterId"></param>
    /// <returns></returns>
    public static bool AddCharacterToAccount(ulong accountId, ulong characterId) {
        return UpdateAccount(accountId, account => {
            account.CharacterIds.Add(characterId);
        });
    }

    /// <summary>
    /// Removes a character from an account.
    /// </summary>
    /// <param name="accountId"></param>
    /// <param name="characterId"></param>
    /// <returns></returns>
    public static bool DeleteCharacterFromAccount(ulong accountId, ulong characterId) {
        return UpdateAccount(accountId, account => {
            account.CharacterIds.Remove(characterId);
        });
    }

    /// <summary>
    /// Adds an infraction to the account with the specified account ID.
    /// </summary>
    /// <param name="accountId">The ID of the account to add the infraction to.</param>
    /// <param name="infractionId">The ID of the infraction to add.</param>
    /// <returns></returns>
    public static bool AddInfractionToAccount(ulong accountId, ulong infractionId) {
        return UpdateAccount(accountId, account => {
            account.InfractionIds.Add(infractionId);
        });
    }

    /// <summary>
    /// Removes an infraction from an account.
    /// </summary>
    /// <param name="accountId">The ID of the account.</param>
    /// <param name="infractionId">The ID of the infraction to remove.</param>
    /// <returns></returns>
    public static bool RemoveInfractionFromAccount(ulong accountId, ulong infractionId) {
        return UpdateAccount(accountId, account => {
            account.InfractionIds.Remove(infractionId);
        });
    }

}
