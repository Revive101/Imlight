/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * ONLINE PLAYER COLLECTION
 * ========================================================================
 * 
 * PURPOSE:
 * Manages the collection of online players, including adding, removing,
 * and retrieving player data from the database.
 * 
 * USAGE EXAMPLE:
 * AddOnlinePlayer(onlinePlayer);
 * RemoveOnlinePlayer(accountId);
 * GetOnlinePlayers();
 * GetOnlinePlayer(characterId);
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 04/28/2025
 */

using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Raven.Client.Documents;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Collections;

public static class OnlinePlayerCollection {

    public const string CollectionName = "OnlinePlayers";
    private static readonly IDocumentStore s_store;

    private static readonly List<OnlinePlayer> s_onlinePlayerCache = [];

    static OnlinePlayerCollection() 
        => s_store = PlayerDatabase.Instance.Store;

    /// <summary>
    /// Adds an online player to the collection.
    /// </summary>
    /// <param name="onlinePlayer">The online player to add.</param>
    public static void AddOnlinePlayer(OnlinePlayer onlinePlayer) {
        // If the ID is already present in the cache, replace it with the new one.
        var existingPlayer = s_onlinePlayerCache
            .FirstOrDefault(x => x is not null && x.AccountId == onlinePlayer.AccountId);
        if (existingPlayer != null) {
            s_onlinePlayerCache.Remove(existingPlayer);
        }
        s_onlinePlayerCache.Add(onlinePlayer);

        // Store the online player in the database.
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
        // Remove from the cache.
        s_onlinePlayerCache.RemoveAll(x => x.AccountId == accountId);

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
        // Remove from the cache.
        s_onlinePlayerCache.RemoveAll(x => x.SessionId == sessionId);

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
    /// Retrieves all online players from the collection.
    /// </summary>
    /// <returns>An array of online players.</returns>
    public static OnlinePlayer[] GetOnlinePlayers() {
        // If the cache is not empty, return the cached players.
        if (s_onlinePlayerCache.Count > 0) {
            return [.. s_onlinePlayerCache];
        }

        // Otherwise, query the database for online players.
        using var session = s_store.OpenSession();

        return [.. session.Query<OnlinePlayer>(collectionName: CollectionName)];
    }

    /// <summary>
    /// Retrieves the online player with the specified account ID from the collection.
    /// </summary>
    /// <param name="characterId">The character ID of the online player to retrieve.</param>
    /// <returns>The online player with the specified account ID, or null if not found.</returns>
    public static OnlinePlayer GetOnlinePlayer(ulong characterId) {
        // Check the cache first.
        var cachedPlayer = s_onlinePlayerCache
            .FirstOrDefault(x => x.CharacterId == characterId);
        if (cachedPlayer != null) {
            return cachedPlayer;
        }

        // If not found in the cache, query the database.
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
        // Check the cache first.
        var cachedPlayers = s_onlinePlayerCache
            .Where(x => x.CurrentZone == zone)
            .ToArray();
        if (cachedPlayers.Length > 0) {
            return cachedPlayers;
        }

        // If not found in the cache, query the database.
        using var session = s_store.OpenSession();

        return [.. session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .Where(x => x.CurrentZone == zone)];
    }

    /// <summary>
    /// Retrieves all online players in the specified realm from the collection.
    /// </summary>
    /// <param name="realm">The realm to filter by.</param>
    /// <returns>An array of online players in the specified realm.</returns>
    public static OnlinePlayer[] GetPlayersInRealm(string realm) {
        // Check the cache first.
        var cachedPlayers = s_onlinePlayerCache
            .Where(x => x.CurrentRealm == realm)
            .ToArray();
        if (cachedPlayers.Length > 0) {
            return cachedPlayers;
        }

        // If not found in the cache, query the database.
        using var session = s_store.OpenSession();

        return [.. session
            .Query<OnlinePlayer>(collectionName: CollectionName)
            .Where(x => x.CurrentRealm == realm)];
    }

    public static void Clear() {
        // Clear the cache.
        s_onlinePlayerCache.Clear();

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
