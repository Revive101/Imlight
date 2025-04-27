/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
        if (PendingFriendRequestsFromCharId.Contains(characterId)
            || HasRelationshipWith(characterId)) {
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
    public bool AddRelationship(Relationship relationship) {
        if (HasRelationshipWith(relationship.FirstPlayerId)
            || HasRelationshipWith(relationship.SecondPlayerId)) {
            return false;
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
        if (HasRelationshipWith(newFrienddId)) {
            return null;
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
        if (HasRelationshipWith(newFrienddId)) {
            return null;
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

        return relationship;
    }

    /// <summary>
    /// Removes a friend from the player's friend list.
    /// </summary>
    /// <param name="characterId">The character ID of the player who is being removed as a friend.</param>
    /// <returns>The relationship between the two players,
    /// or null if the player is not a friend.</returns>
    public Relationship RemoveRelationship(ulong characterId) {
        if (!TryGetRelationship(characterId, out var relationship)) {
            return null;
        }

        Relationships.RemoveAll(x => x.FirstPlayerId == characterId || x.SecondPlayerId == characterId);

        return relationship;
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
    public bool IsBlocked(ulong characterId) {
        if (!TryGetRelationship(characterId, out var relationship)) {
            return false;
        }

        return relationship.Blocked;
    }

    BehaviorInstance IClientBehaviorProvider<BehaviorInstance>.GetClientBehaviorInstance() 
        => throw new NotImplementedException();
}