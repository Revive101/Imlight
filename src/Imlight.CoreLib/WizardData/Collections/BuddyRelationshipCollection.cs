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
 *
 * ========================================================================
 * BUDDY RELATIONSHIP COLLECTION.
 * ========================================================================
 * 
 * PURPOSE:
 * Manages database operations for buddy relationships between players.
 * 
 * USAGE EXAMPLE:
 * var relationship = new Relationship {
 *     FirstPlayerId = 123456789,
 *     SecondPlayerId = 987654321,
 *     IsBrokenUp = false
 * };
 * BuddyRelationshipCollection.AddRelationship(relationship);
 * 
 * NOTE:
 * Flow to adding a friend:
    1. Player A clicks on player B's character and selects "Add Friend".
        Player A sends GAME_5_PROTOCOL.MSG_BUDDYREQUESTADD to the server. Player A keeps track of player B's ID.
    2. Server forwards the request to player B with CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTADDFWD.
    3. Player B receives the request, and adds it to their pending friend requests. They will see a notification in the client.
    4. Player B can now accept or deny the request.
        a. If they accept, they send GAME_5_PROTOCOL.MSG_BUDDYREQUESTACCEPT to the server.
        b. If they deny, they send GAME_5_PROTOCOL.MSG_BUDDYREQUESTDENY to the server.
    5. Server forwards the response to player A with CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTREPLYFWD. If the sender
         is offline, we will have to go to the database directly to add the friend.
    6. Player A receives the response. If the response is an acceptance, they will add the friend to their list.
        Player A's game client will see a notification that the friend request has been accepted.
 * 
 * TODO:
 * 
 * Created by: JOOTY
 * Version: KALI 1.0
 * Last Updated: 04/27/2025
 */

using System.Collections.Generic;
using System.Linq;
using Raven.Client.Documents;
using Imlight.CoreLib.WizardData.Databases;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Implementations;

namespace Imlight.CoreLib.WizardData.Collections;

public static class BuddyRelationshipCollection {

    public const string CollectionName = "BuddyRelationships";
    private const uint KeyExpireTimeInHours = 4383; // 6 months
    private static readonly IDocumentStore s_store;

    static BuddyRelationshipCollection() {
        s_store = PlayerDatabase.Instance.Store;
    }

    /// <summary>
    /// Adds a relationship to the collection. If a relationship already exists between the two players, it will be restored.
    /// </summary>
    /// <param name="relationship">The relationship to add.</param>
    /// <returns>The added relationship, if one was added rather than restored. Otherwise, the restored relationship.</returns>
    public static Relationship AddRelationship(Relationship relationship) {
        using var session = s_store.OpenSession();

        // Check to see if we can find an existing relationship. If we can find one, we'll instead restore it
        // rather than creating a new one.
        var firstId = relationship.FirstPlayerId;
        var secondId = relationship.SecondPlayerId;
        var existingRelationship = session.Query<Relationship>(collectionName: CollectionName)
            .FirstOrDefault(r => (r.FirstPlayerId == firstId  && r.SecondPlayerId == secondId)
                              || (r.FirstPlayerId == secondId && r.SecondPlayerId == firstId));

        if (existingRelationship != null) {
            existingRelationship.IsBrokenUp = false;

            // Update the metadata so that the entry no longer expires.
            var existingMetadata = session.Advanced.GetMetadataFor(existingRelationship);
            existingMetadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
            existingMetadata.Remove(Raven.Client.Constants.Documents.Metadata.Expires);

            session.SaveChanges();

            return existingRelationship;
        }

        session.Store(relationship);
        var metadata = session.Advanced.GetMetadataFor(relationship);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;

        session.SaveChanges();

        return relationship;
    }

    /// <summary>
    /// Updates a relationship in the collection.
    /// </summary>
    /// <param name="relationship">The relationship to update.</param>
    public static void UpdateRelationship(Relationship relationship) {
        using var session = s_store.OpenSession();

        // Get the relationship using either the first or second player ID.
        var existingRelationship = session.Query<Relationship>(collectionName: CollectionName)
            .Where(r => r.FirstPlayerId == relationship.FirstPlayerId && r.SecondPlayerId == relationship.SecondPlayerId)
            .FirstOrDefault();

        if (existingRelationship == null) {
            return;
        }   

        // Update the relationship.
        existingRelationship.Blocked = relationship.Blocked;
        existingRelationship.IsBrokenUp = relationship.IsBrokenUp;

        var metadata = session.Advanced.GetMetadataFor(existingRelationship);
        metadata[Raven.Client.Constants.Documents.Metadata.Collection] = CollectionName;
        metadata[Raven.Client.Constants.Documents.Metadata.Expires] = KeyExpireTimeInHours;

        session.SaveChanges();
    }

    /// <summary>
    /// Gets all relationships for a player.
    /// </summary>
    /// <param name="playerId">The player ID to get relationships for.</param>
    /// <returns>The relationships for the player.</returns>
    public static List<Relationship> GetRelationshipsForPlayer(ulong playerId) {
        using var session = s_store.OpenSession();

        return [.. session.Query<Relationship>(collectionName: CollectionName)
            .Where(r => r.FirstPlayerId == playerId || r.SecondPlayerId == playerId)];
    }

    /// <summary>
    /// Gets all buddies for a player.
    /// </summary>
    /// <param name="playerId">The player ID to get buddies for.</param>
    /// <returns>The buddies for the player.</returns>
    public static Wizard[] GetBuddiesForWizard(ulong playerId) {
        using var session = s_store.OpenSession();

        var relationships = session.Query<Relationship>(collectionName: CollectionName)
            .Where(r => r.FirstPlayerId == playerId || r.SecondPlayerId == playerId)
            .ToList();

        var buddies = new List<Wizard>();
        foreach (var relationship in relationships) {
            var buddyId = relationship.FirstPlayerId == playerId
                ? relationship.SecondPlayerId
                : relationship.FirstPlayerId;

            var buddy = WizardCollection.GetCharacter(buddyId);
            if (buddy != null) {
                buddies.Add(buddy);
            }
        }

        return [.. buddies];
    }

}