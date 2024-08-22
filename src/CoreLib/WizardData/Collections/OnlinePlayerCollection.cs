/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Raven.Client.Documents;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Collections;

public static class OnlinePlayerCollection {
    public const string CollectionName = "OnlinePlayers";
    private static readonly IDocumentStore s_store;

    static OnlinePlayerCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds an online player to the collection.
    /// </summary>
    /// <param name="onlinePlayer">The online player to add.</param>
    public static void AddOnlinePlayer(OnlinePlayer onlinePlayer) {
        using var session = s_store.OpenSession();

        session.Store(onlinePlayer);
        var metadata = session.Advanced.GetMetadataFor(onlinePlayer);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();
    }

    /// <summary>
    /// Removes an online player from the collection based on the specified account ID.
    /// </summary>
    /// <param name="accountId">The character ID of the online player to remove.</param>
    public static void RemoveOnlinePlayer(ulong accountId) {
        using var session = s_store.OpenSession();

        var onlinePlayer = session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .FirstOrDefault(x => x.AccountId == accountId);
        if (onlinePlayer != null) {
            session.Delete(onlinePlayer);
            session.SaveChanges();
        }
    }

    /// <summary>
    /// Removes an online player from the collection based on the specified session ID.
    /// </summary>
    /// <param name="sessionId">The session ID of the online player to remove.</param>
    public static void RemoveOnlinePlayer(ushort sessionId) {
        using var session = s_store.OpenSession();

        var onlinePlayer = session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .FirstOrDefault(x => x.SessionId == sessionId);
        if (onlinePlayer != null) {
            session.Delete(onlinePlayer);
            session.SaveChanges();
        }
    }

    /// <summary>
    /// Retrieves an array of online friends for the specified account ID.
    /// </summary>
    /// <param name="accountId">The account ID of the player.</param>
    /// <returns>An array of <see cref="OnlinePlayer"/> objects representing the online friends.</returns>
    public static OnlinePlayer[] GetOnlineFriends(ulong accountId) {
        using var session = s_store.OpenSession();

        var onlinePlayer = session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .FirstOrDefault(x => x.AccountId == accountId);
        if (onlinePlayer != null) {
            return session
                .Query<OnlinePlayer>(collectionName: CollectionName)
                .Where(x => onlinePlayer.Friends.Contains(x.AccountId))
                .ToArray();
        }

        return System.Array.Empty<OnlinePlayer>();
    }

    /// <summary>
    /// Retrieves all online players from the collection.
    /// </summary>
    /// <returns>An array of online players.</returns>
    public static OnlinePlayer[] GetOnlinePlayers() {
        using var session = s_store.OpenSession();

        return session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .ToArray();
    }

    /// <summary>
    /// Retrieves the online player with the specified account ID from the collection.
    /// </summary>
    /// <param name="characterId">The character ID of the online player to retrieve.</param>
    /// <returns>The online player with the specified account ID, or null if not found.</returns>
    public static OnlinePlayer GetOnlinePlayer(ulong characterId) {
        using var session = s_store.OpenSession();

        return session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .FirstOrDefault(x => x.CharacterId == characterId);
    }

    /// <summary>
    /// Retrieves all online players in the specified zone from the collection.
    /// </summary>
    /// <param name="zone">The zone to filter by.</param>
    /// <returns>An array of online players in the specified zone.</returns>
    public static OnlinePlayer[] GetPlayersInZone(string zone) {
        using var session = s_store.OpenSession();

        return session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .Where(x => x.CurrentZone == zone)
            .ToArray();
    }

    /// <summary>
    /// Retrieves all online players in the specified realm from the collection.
    /// </summary>
    /// <param name="realm">The realm to filter by.</param>
    /// <returns>An array of online players in the specified realm.</returns>
    public static OnlinePlayer[] GetPlayersInRealm(string realm) {
        using var session = s_store.OpenSession();

        return session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .Where(x => x.CurrentRealm == realm)
            .ToArray();
    }

    public static void Clear() {
        using var session = s_store.OpenSession();

        var onlinePlayers = session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .ToArray();
        foreach (var onlinePlayer in onlinePlayers) {
            session.Delete(onlinePlayer);
        }

        session.SaveChanges();
    }
}
