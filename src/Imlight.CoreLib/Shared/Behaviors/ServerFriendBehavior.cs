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
 * SERVER FRIEND BEHAVIOR
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player friend requests, buddy list, and relationship status.
 * 
 * USAGE EXAMPLE:
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

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.Shared.Utilities;
using Imcodec.IO;

namespace Imlight.CoreLib.Shared.Behaviors;

public class ServerFriendBehavior : IClientBehaviorProvider<BehaviorInstance> {

    [JsonIgnore] public bool NoTransfer { get; set; } = true;

    [JsonIgnore] public List<Relationship> Relationships { get; set; } = new();

    public readonly List<ulong> PendingFriendRequestsFromCharId = new();

    /// <summary>
    /// Adds a pending friend request from a character.
    /// </summary>
    /// <param name="characterId">The character ID of the player who sent the friend request.</param>
    /// <returns>True if the friend request was added, false if the friend request already exists.</returns>
    public bool AddPendingFriendRequest(ulong characterId) {
        if (PendingFriendRequestsFromCharId.Contains(characterId)) {
            return false;
        }

        PendingFriendRequestsFromCharId.Add(characterId);

        return true;
    }

    /// <summary>
    /// Removes a pending friend request from a character.
    /// </summary>
    /// <param name="characterId">The character ID of the player who sent the friend request.</param>
    /// <returns>True if the friend request was removed, false if the friend request does not exist.</returns>
    public bool RemovePendingFriendRequest(ulong characterId)
        => PendingFriendRequestsFromCharId.RemoveAll(x => x == characterId) > 0;

    /// <summary>
    /// Adds a relationship to the player's friend list.
    /// </summary>
    /// <param name="relationship">The relationship to add.</param>
    /// <returns>True if the relationship was added, false if the relationship already exists.</returns>
    public bool AddOrUpdateRelationship(Relationship relationship) {
        // Upsert the relationship if it already exists.
        var existingRelationship = Relationships
            .FirstOrDefault(x => x.FirstPlayerId == relationship.FirstPlayerId && x.SecondPlayerId == relationship.SecondPlayerId);
        if (existingRelationship != null) {
            var index = Relationships.IndexOf(existingRelationship);
            Relationships[index] = relationship;

            return true;
        }

        Relationships.Add(relationship);

        return true;
    }

    /// <summary>
    /// Adds a friend to the player's friend list.
    /// </summary>
    /// <param name="ownerId">The character ID of the player who is adding the friend.</param>
    /// <param name="newFrienddId">The character ID of the player who is being added as a friend.</param>
    /// <returns>The relationship between the two players,
    /// or null if the player is already a friend or the friend request does not exist.</returns>
    public Relationship AddFriend(ulong ownerId, ulong newFrienddId) {
        // Upsert the relationship if it already exists.
        var existingRelationship = Relationships
            .FirstOrDefault(x => x.FirstPlayerId == newFrienddId && x.SecondPlayerId == ownerId);
        if (existingRelationship != null) {
            existingRelationship.FirstPlayerId = ownerId;
            existingRelationship.SecondPlayerId = newFrienddId;
            existingRelationship.IsBrokenUp = false;
            existingRelationship.Blocked = false;

            return existingRelationship;
        }

        PendingFriendRequestsFromCharId.Remove(newFrienddId);

        // Create a new relationship.
        var relationship = new Relationship {
            FirstPlayerId = ownerId,
            SecondPlayerId = newFrienddId,
        };
        Relationships.Add(relationship);

        return relationship;
    }

    /// <summary>
    /// Adds a true friend to the player's friend list.
    /// </summary>
    /// <param name="ownerId">The character ID of the player who is adding the friend.</param>
    /// <param name="newFrienddId">The character ID of the player who is being added as a friend.</param>
    /// <returns>The relationship between the two players,
    /// or null if the player is already a friend or the friend request does not exist.</returns>
    public Relationship AddTrueFriend(ulong ownerId, ulong newFrienddId) {
        // Upsert the relationship if it already exists.
        var existingRelationship = Relationships
            .FirstOrDefault(x => x.FirstPlayerId == newFrienddId && x.SecondPlayerId == ownerId);
        if (existingRelationship != null) {
            existingRelationship.FirstPlayerId = ownerId;
            existingRelationship.SecondPlayerId = newFrienddId;
            existingRelationship.IsBrokenUp = false;
            existingRelationship.Blocked = false;
            existingRelationship.AddedViaTrueFriend = true;

            return existingRelationship;
        }

        PendingFriendRequestsFromCharId.Remove(newFrienddId);

        // Create a new relationship.
        var relationship = new Relationship {
            FirstPlayerId = ownerId,
            SecondPlayerId = newFrienddId,
            AddedViaTrueFriend = true,
        };
        Relationships.Add(relationship);

        return relationship;
    }

    /// <summary>
    /// Adds a blocked player to the player's friend list.
    /// </summary>
    /// <param name="ownerId">The character ID of the player who is adding the friend.</param>
    /// <param name="newFrienddId">The character ID of the player who is being added as a friend.</param>
    /// <returns>The relationship between the two players,
    /// or null if the player is already a friend or the friend request does not exist.</returns>
    public Relationship AddBlockedPlayer(ulong ownerId, ulong newFrienddId) {
        if (!HasRelationshipWith(newFrienddId)
            || !PendingFriendRequestsFromCharId.Contains(newFrienddId)) {
            return null;
        }

        PendingFriendRequestsFromCharId.Remove(newFrienddId);

        // Create a new relationship.
        var relationship = new Relationship {
            FirstPlayerId = ownerId,
            SecondPlayerId = newFrienddId,
            Blocked = true,
        };
        Relationships.Add(relationship);

        // Upsert the relationship if it already exists.
        var existingRelationship = Relationships
            .FirstOrDefault(x => x.FirstPlayerId == newFrienddId && x.SecondPlayerId == ownerId);
        if (existingRelationship != null) {
            existingRelationship.FirstPlayerId = ownerId;
            existingRelationship.SecondPlayerId = newFrienddId;
            existingRelationship.IsBrokenUp = false;
            existingRelationship.Blocked = true;

            return existingRelationship;
        }

        return relationship;
    }

    /// <summary>
    /// Break up a relationship with a character.
    /// </summary>
    /// <param name="characterId">The character ID of the player who is being removed as a friend.</param>
    /// <returns>The relationship between the two players,
    /// or null if the player is not a friend.</returns>
    public Relationship Breakup(ulong characterId) {
        if (!TryGetRelationship(characterId, out var relationship)) {
            return null;
        }

        relationship.IsBrokenUp = true;

        return relationship;
    }

    /// <summary>
    /// Ignores a relationship with a character.
    /// </summary>
    /// <param name="ownerId">The character ID of the player who is ignoring.</param>
    /// <param name="targetId">The character ID of the player who is being ignored.</param>
    /// <returns>The relationship between the two players.</returns>
    public Relationship Ignore(ulong ownerId, ulong targetId) {
        // Create the relationship if it doesn't already exist.
        if (!TryGetRelationship(targetId, out var relationship)) {
            relationship = new Relationship {
                FirstPlayerId = ownerId,
                SecondPlayerId = targetId,
            };
            Relationships.Add(relationship);
        }

        relationship.Blocked = true;

        return relationship;
    }

    /// <summary>
    /// Unignores a relationship with a character, clearing the blocked flag.
    /// </summary>
    /// <param name="characterId">The character ID of the player who is being unignored.</param>
    /// <returns>The relationship between the two players, or null if no relationship exists.</returns>
    public Relationship Unignore(ulong characterId) {
        if (!TryGetRelationship(characterId, out var relationship)) {
            return null;
        }

        relationship.Blocked = false;

        return relationship;
    }

    /// <summary>
    /// Gets all the ignored players.
    /// </summary>
    /// <param name="ownerId">The character ID of the player whose ignore list is being requested.</param>
    /// <returns>A list of ignored players.</returns>
    public IgnoreEntryDataList GetIgnoredPlayers(ulong ownerId) {
        var ignoreList = new IgnoreEntryDataList() {
            m_ignoreDataList = []
        };

        foreach (var ignoredRelationship in Relationships
                     .Where(x => x.Blocked && !x.IsBrokenUp)) {
            var otherPlayerID = ignoredRelationship.FirstPlayerId == ownerId
                ? ignoredRelationship.SecondPlayerId
                : ignoredRelationship.FirstPlayerId;

            var otherPlayerWizardData = WizardCollection.GetCharacterUnloaded(otherPlayerID);
            var otherPlayerWizardHexName = otherPlayerWizardData.PlayerNameBehavior.GetWizardNameAsByteHexString();
            var otherPlayerByteName = DataManipulation.SpacedHexStringToBytes(otherPlayerWizardHexName);

            var ignoreEntry = new IgnoreEntryData {
                m_ignoreName = new ByteString(otherPlayerByteName),
                m_characterID = otherPlayerID,
                m_gameObjectID = otherPlayerID,
            };

            ignoreList.m_ignoreDataList.Add(ignoreEntry);
        }

        return ignoreList;
    }

    /// <summary>
    /// Attempts to get a relationship with a character.
    /// </summary>
    /// <param name="characterId">The character ID of the player who is being checked.</param>
    /// <param name="relationship">The relationship between the two players.</param>
    /// <returns>True if the player has the other added as a friend.</returns>
    public bool TryGetRelationship(ulong characterId, out Relationship relationship) {
        if (!HasRelationshipWith(characterId)) {
            relationship = null;
            return false;
        }

        relationship = Relationships
            .FirstOrDefault(x => x.FirstPlayerId == characterId || x.SecondPlayerId == characterId);
            
        return true;
    }

    /// <summary>
    /// Removes a friend from the player's friend list.
    /// </summary>
    /// <param name="characterId">The character ID of the player who is being removed as a friend.</param>
    /// <returns>True, if the wizard has the other added as a friend.</returns>
    public bool HasRelationshipWith(ulong characterId)
        => Relationships.Any(x => x.FirstPlayerId == characterId || x.SecondPlayerId == characterId);

    /// <summary>
    /// Checks if the player has a pending friend request from a character.
    /// </summary>
    /// <param name="characterId">The character ID of the player who sent the friend request.</param>
    /// <returns>True if the player has a pending friend request from the character.</returns>
    public bool HasPendingFriendRequest(ulong characterId)
        => PendingFriendRequestsFromCharId.Contains(characterId);

    /// <summary>
    /// Checks if the player has a certain other player blocked.
    /// </summary>
    /// <param name="characterId">The character ID of the player who is being checked.</param>
    /// <returns>True if the player has the other player blocked.</returns>
    public bool HasPlayerBlocked(ulong characterId) {
        if (!TryGetRelationship(characterId, out var relationship)) {
            return false;
        }

        return relationship.Blocked;
    }

    BehaviorInstance IClientBehaviorProvider<BehaviorInstance>.GetClientBehaviorInstance() 
        => throw new NotImplementedException();
}