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
 * FRIENDS SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player friend requests, buddy list, and relationship status.
 * 
 * USAGE EXAMPLE:
 * Internal service handling various social-related messages and actions 
 * within the game server's session management system.
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

    - The game client uses the Char ID and GameObject ID interchangeably:
        - Instances where the GameObject ID is used:
            - GAME_5_PROTOCOL.MSG_BUDDYSTATS.TargetCharacterGID
            - GAME_5_PROTOCOL.MSG_BUDDYSTATS.BuddyID
            - GAME_5_PROTOCOL.MSG_BUDDYENTRY.ListOwnerGID
            - GAME_5_PROTOCOL.MSG_BUDDYLISTCOMPLETE.ListOwnerGID
            - GAME_5_PROTOCOL.MSG_BUDDYSTATUSUPDATE.ListOwnerGID
            - GAME_5_PROTOCOL.MSG_BUDDYREQUESTACCEPT.SourceObjectID
            - GAME_5_PROTOCOL.MSG_BUDDYREQUESTACCEPT.DestObjectID
            - GAME_5_PROTOCOL.MSG_BUDDYREQUESTADD.EntryGID
        - Instances where the Char ID is used:
            - GAME_5_PROTOCOL.MSG_BUDDYREQUESTACCEPT.ListOwnerGID
            - GAME_5_PROTOCOL.MSG_BUDDYREQUESTACCEPT.EntryGID
            - GAME_5_PROTOCOL.MSG_BUDDYSTATUSUPDATE.EntryGID

    - The game client will not request buddy stats (`MSG_BUDDYSTATS`) if the friend is offline

    ============ BUDDY STATS ============ 

    !!! IN PROGRESS !!!

    StatBlock    : 
        - ObjectSerializer (StatefulFlags)
        - Type: WizGameStats
    EquipBlock   : 
        - ObjectSerializer (StatefulFlags)
        - Type: ClientWizEquipmentBehavior

    =====================================
 * 
 * TODO:
 * - For `MSG_BUDDYSTATS`, we need to implement the CRC32 hash check. Currently, we just send the stats regardless.
 * - Implement the `PreviousName` field in `MSG_BUDDYENTRY`.
 * - Player ignoring
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 04/27/2025
 */

using System;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.Math;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class FriendsService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const byte OFFLINE_STATUS_CODE = 1;
    private const byte ONLINE_STATUS_CODE = 4;
    private const float QUERY_TELEPORT_WIZARD_TIMEOUT_IN_SECONDS = 2;
    private readonly uint _englishLocaleHash = StringHash.Compute("English");

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new FriendsService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYREQUESTLIST))]
    private void ReceiveBuddyRequestList(GAME_5_PROTOCOL.MSG_BUDDYREQUESTLIST message) {
        // When the player logs into the game, their client will request a list of their buddies.
        // We will iterate through the player's friends and send them an entry for each.
        var wizard = GetActiveWizard();
        var charId = wizard.GameObject?.m_globalID ?? wizard.CharId;

        // Iterate through the player's friends and send them an entry.
        var buddies = BuddyRelationshipCollection.GetBuddiesForWizard(charId);
        foreach (var buddy in buddies.Where(buddy => buddy != null)) {
            // When players add each other, they create a "Relationship." This is a record of their
            // interactions. When a player unfriends/blocks/reports another, that relationship is still exist
            // but is marked as "broken up." 
            if (!wizard.FriendsBehavior.TryGetRelationship(buddy.CharId, out var relationship)
                || relationship.IsBrokenUp) {
                // Ignore the relationship if it is broken up.
                Logger.Debug("{0} has a friend named {1} (ID: {2}), but the relationship is broken up.",
                    Logger.Args(wizard.PlayerNameBehavior.GetWizardName(),
                    buddy.PlayerNameBehavior.GetWizardName(),
                    buddy.CharId)
                );

                continue;
            }
            else if (relationship.Blocked) {
                // Ignore the relationship if it is blocked.
                Logger.Debug("{0} has a friend named {1} (ID: {2}), but the relationship is blocked.",
                    Logger.Args(wizard.PlayerNameBehavior.GetWizardName(),
                    buddy.PlayerNameBehavior.GetWizardName(),
                    buddy.CharId)
                );

                continue;
            }
            else if (!relationship.IsBrokenUp && !relationship.Blocked) {
                // This relationship is valid. We can send it to the client.
                SendBuddyEntry(buddy, relationship, charId);
            }
            else {
                Logger.Warning("{0} has a friend with character ID {1}, but the relationship was not found.",
                    Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), buddy.CharId));
            }
        }

        SendBuddyListEnd(charId);
        InformBuddiesOfStatusChange(true);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYSTATS))]
    private void ReceiveBuddyStats(GAME_5_PROTOCOL.MSG_BUDDYSTATS message) {
        // When the player clicks on a friend in their buddy list, they will request the friend's stats (`MSG_BUDDYSTATS`).
        // The client caches each friends stats and sends the server the CRC32 hash of their cached stats.
        // We will hash the respective fields on the server side and compare them with the client's hash.
        // If the hashes do not match, we will send a new byte blob containing the serialized data to update the client's cache.
        if (message.BuddyID == 0) {
            return;
        }

        // TODO: Imlight currently doesn't care about the CRC. It will send the stats regardless.
        var buddyFromDatabase = WizardCollection.GetCharacter(message.BuddyID);
        if (buddyFromDatabase is null) {
            Logger.Error("Player {0} requested stats for character ID {1}, but the character was not found.",
                Logger.Args(GetActiveWizard().PlayerNameBehavior.GetWizardName(), message.BuddyID));

            return;
        }

        var returnMessage = new GAME_5_PROTOCOL.MSG_BUDDYSTATS {
            BuddyID = message.BuddyID,
            Level = (uint) buddyFromDatabase.MagicSchoolBehavior.Level,
            School = (uint) buddyFromDatabase.MagicSchoolBehavior.MagicSchool,
            Gender = 0, // TODO: ?? Is 0 female? Male?

            // Set each CRC as '1' to inform the client we're giving it new data.
            StatBlockCRC = 1,
        };

        var statBlock = GetBuddyGameStats(buddyFromDatabase);

        // Serialize each of the blocks.
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.SerializeFlags
        );

        // Check if the serialization was successful.
        if (!serializer.Serialize(statBlock, 1, out var statBlockBytes)) {
            Logger.Error("Player {0} requested stats for character ID {1}, but the serialization failed.",
                Logger.Args(GetActiveWizard().PlayerNameBehavior.GetWizardName(), message.BuddyID));

            return;
        }
        else {
            // Set the serialized data in the return message.
            returnMessage.StatBlock = statBlockBytes;

            // Log.
            var wizardName = GetActiveWizard().PlayerNameBehavior.GetWizardName();
            Logger.Debug("{0} has requested stats for character ID {1}.",
                Logger.Args(wizardName, message.BuddyID));
        }


        SendToSocket(returnMessage);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYREQUESTADD))]
    private void ReceiveBuddyRequestAdd(GAME_5_PROTOCOL.MSG_BUDDYREQUESTADD message) {
        // (SEE TOP OF FILE FOR FLOW) Step 1:
        // The player client has clicked on another player and selected "Add Friend".
        // We need to forward this request to the recipient. We will find the online actor for the recipient
        // and send the internal message "MSG_BUDDYREQUESTADDFWD" to them.
        var wizard = GetActiveWizard();

        // Check to see if the recipient is online. If not, there is naught we can do.
        if (!TryGetOnlinePlayer(message.EntryGID, out var onlinePlayer)) {
            var wizardName = wizard.PlayerNameBehavior.GetWizardName();
            Logger.Warning("{0} tried to add character ID {1} as a friend, but the character is not online.",
                Logger.Args(wizardName, message.EntryGID));

            var errorMsg = new GAME_5_PROTOCOL.MSG_BUDDYREQUESTERROR {
                ListOwnerGID = wizard.CharId,
                EntryGID = message.EntryGID,
                Error = 1
            };
            SendToSocket(errorMsg);

            return;
        }

        // Add the pending request to the sender. When we get a response from the recipient, we'll know it's valid.
        wizard.AddPendingFriendRequest(message.EntryGID);

        // Forward the request to the recipient.
        // (SEE TOP OF FILE FOR FLOW) Step 2
        var fwdMsg = new CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTADDFWD {
            ListOwnerGID = wizard.CharId,
            EntryGID = message.EntryGID,
            OwnerName = wizard.PlayerNameBehavior.GetWizardName(),
            OwnerLevel = (byte) wizard.MagicSchoolBehavior.Level,
            OwnerSchool = wizard.MagicSchoolBehavior.MagicSchool.ToString()
        };
        Context.ActorSelection(onlinePlayer.ActorPath).Tell(fwdMsg);

        Logger.Debug("{0} has sent a friend request to character ID {1}.",
            Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.EntryGID));
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTADDFWD))]
    private void ReceiveBuddyRequestAddFwd(CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTADDFWD message) {
        // (SEE TOP OF FILE FOR FLOW) Step 3:
        // This actor has received a friend request from another player. We need to add it to the pending requests,
        // and inform the game client.
        var wizard = GetActiveWizard();

        // Is the owner ID.. ourselves?
        if (message.ListOwnerGID == wizard.CharId) {
            Logger.Warning("{0} tried to add themselves as a friend.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName()));

            return;
        }

        // Check to see if we already have this friend request pending.
        if (wizard.FriendsBehavior.HasPendingFriendRequest(message.ListOwnerGID)) {
            Logger.Warning("{0} tried to add character ID {1} as a friend, but the request is already pending.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.ListOwnerGID));

            return;
        }

        // Add the pending request to the recipient. When we get a response from the sender, we'll know it's valid.
        wizard.AddPendingFriendRequest(message.ListOwnerGID);

        // Inform the game client. We will now await the player's response.
        var clientMsg = new GAME_5_PROTOCOL.MSG_BUDDYREQUESTADD {
            ListOwnerGID = message.ListOwnerGID,
            EntryGID = message.EntryGID,
            OwnerName = message.OwnerName,
            OwnerLevel = message.OwnerLevel,
            OwnerSchool = message.OwnerSchool
        };
        SendToSocket(clientMsg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYREQUESTACCEPT))]
    private void ReceiveBuddyRequestAccept(GAME_5_PROTOCOL.MSG_BUDDYREQUESTACCEPT message) {
        // (SEE TOP OF FILE FOR FLOW) Step 4a: The recipient's game client has accepted the friend request.
        var wizard = GetActiveWizard();

        // Ensure that this wizard was even pending. If not, log an error and return.
        if (!wizard.RemovePendingFriendRequest(message.ListOwnerGID)) {
            Logger.Error("{0} tried to add character ID {1} as a friend, but the request was not pending.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.ListOwnerGID));

            return;
        }

        // Add the wizard as a friend. If they are already friends, log an error and return.
        if (!wizard.AddOrRepairRelationship(message.ListOwnerGID)) {
            Logger.Error("{0} could not add or update the relationship with character ID {1}.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.ListOwnerGID));

            return;
        }

        // Forward the acceptance to the sender.
        // (SEE TOP OF FILE FOR FLOW) Step 5a
        if (!TryGetOnlinePlayer(message.ListOwnerGID, out var onlinePlayer)) {
            // The sender is not online. We'll have to go to the database directly to add the friend.
            var offlineWizard = WizardCollection.GetCharacter(message.ListOwnerGID);

            // Ensure that this wizard was even pending. If not, log an error and return.
            if (!offlineWizard.RemovePendingFriendRequest(wizard.CharId)) {
                Logger.Error("{0} tried to add character ID {1} as a friend, but the request was not pending.",
                    Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.ListOwnerGID));

                return;
            }

            WizardCollection.UpdateCharacterFriendBehavior(offlineWizard);
        }
        else {
            if (!wizard.FriendsBehavior.TryGetRelationship(message.ListOwnerGID, out var relationship)) {
                Logger.Error("{0} tried to add character ID {1} as a friend, but the relationship was not found.",
                    Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.ListOwnerGID));

                return;
            }

            // The player is still online, so we can forward the acceptance.
            var fwdMsg = new CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTREPLYFWD {
                ListOwnerGID = message.ListOwnerGID,
                EntryGID = wizard.CharId,
                Accept = true,
                NewRelationship = relationship
            };
            Context.ActorSelection(onlinePlayer.ActorPath).Tell(fwdMsg);
        }

        // Echo the packet back to the client.
        SendToSocket(message);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYREQUESTDENY))]
    private void ReceiveBuddyRequestDeny(GAME_5_PROTOCOL.MSG_BUDDYREQUESTDENY message) {
        // (SEE TOP OF FILE FOR FLOW) Step 4b: The recipient has denied the friend request.
        var wizard = GetActiveWizard();

        // Ensure that this wizard was even pending. If not, log an error and return.
        if (!wizard.RemovePendingFriendRequest(message.ListOwnerGID)) {
            Logger.Error("{0} tried to deny a friend request from character ID {1}, but the request was not pending.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.ListOwnerGID));

            return;
        }

        // Forward the acceptance to the sender.
        // (SEE TOP OF FILE FOR FLOW) Step 5b
        if (!TryGetOnlinePlayer(message.ListOwnerGID, out var onlinePlayer)) {
            // The sender is not online. We'll have to go to the database directly to remove the pending request.
            var offlineWizard = WizardCollection.GetCharacter(message.ListOwnerGID);

            // Ensure that this wizard was even pending. If not, log an error and return.
            if (!offlineWizard.RemovePendingFriendRequest(wizard.CharId)) {
                Logger.Error("{0} tried to deny a friend request from character ID {1}, but the request was not pending.",
                    Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.ListOwnerGID));

                return;
            }

            WizardCollection.UpdateCharacterFriendBehavior(offlineWizard);
        }
        else {
            // The player is still online, so we can forward the denial.
            var fwdMsg = new CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTREPLYFWD {
                ListOwnerGID = message.ListOwnerGID,
                EntryGID = wizard.CharId,
                Accept = false
            };
            Context.ActorSelection(onlinePlayer.ActorPath).Tell(fwdMsg);
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTREPLYFWD))]
    private void ReceiveBuddyRequestReplyFwd(CHARACTER_103_PROTOCOL.MSG_BUDDYREQUESTREPLYFWD message) {
        // (SEE TOP OF FILE FOR FLOW) Step 6:
        // We are informing player A that player B has accepted or denied their friend request.
        var myWizard = GetActiveWizard();
        IMessage clientMsg;

        myWizard.RemovePendingFriendRequest(message.EntryGID);

        if (message.Accept) {
            var entryWizard = WizardCollection.GetCharacter(message.EntryGID);
            var epochInSeconds = (uint) DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Log
            var myName = myWizard.PlayerNameBehavior.GetWizardName();
            var entryName = entryWizard.PlayerNameBehavior.GetWizardName();
            Logger.Debug("{0} (ID {1}) has received a friend request reply from {2} (ID {3}). They have accepted.",
                Logger.Args(myName, myWizard.CharId, entryName, entryWizard.CharId));

            // Add the wizard as a friend. If they are already friends, log an error and return.
            if (!myWizard.AddOrRepairRelationship(message.NewRelationship)) {
                Logger.Error("{0} could not add or update the relationship with character ID {1}.",
                    Logger.Args(myWizard.PlayerNameBehavior.GetWizardName(), message.EntryGID));

                return;
            }

            var myWizardNameHex = myWizard.PlayerNameBehavior.GetWizardNameAsByteHexString();
            var myWizardNameBytes = DataManipulation.SpacedHexStringToBytes(myWizardNameHex);
            var entryWizardNameHex = entryWizard.PlayerNameBehavior.GetWizardNameAsByteHexString();
            var entryWizardNameBytes = DataManipulation.SpacedHexStringToBytes(entryWizardNameHex);

            // Inform the game client that the friend request has been accepted.
            clientMsg = new GAME_5_PROTOCOL.MSG_BUDDYREQUESTACCEPT {
                ListOwnerGID = message.ListOwnerGID,
                EntryGID = message.EntryGID,
                OwnerName = myWizardNameBytes,
                EntryName = entryWizardNameBytes,
                SourceObjectID = myWizard.GameObject?.m_globalID ?? myWizard.CharId,
                DestObjectID = entryWizard.GameObject?.m_globalID ?? entryWizard.CharId,
                Error = 0,
                Permissions = (uint) entryWizard.Account.GetAccountFlags(),
                EntryLocale = _englishLocaleHash,
                FriendInfo = 197120,           // TODO: What is this?
                FriendDate = epochInSeconds,
                FriendStatusDate = epochInSeconds,
            };
        }
        else {
            // Log
            var myName = myWizard.PlayerNameBehavior.GetWizardName();
            Logger.Debug("{0} (ID {1}) has received a friend request reply from character ID {2}. They have denied.",
                Logger.Args(myName, myWizard.CharId, message.EntryGID));

            // Inform the game client that the friend request has been denied.
            clientMsg = new GAME_5_PROTOCOL.MSG_BUDDYREQUESTDENY {
                ListOwnerGID = message.ListOwnerGID,
                EntryGID = message.EntryGID
            };
        }

        SendToSocket(clientMsg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYREQUESTDROP))]
    private void ReceiveBuddyDrop(GAME_5_PROTOCOL.MSG_BUDDYREQUESTDROP message) {
        // A player wants to remove a friend from their list. Start by removing the friend from ourselves first.
        var wizard = GetActiveWizard();

        if (!wizard.RemoveFriend(message.EntryGID)) {
            Logger.Error("{0} tried to remove character ID {1} as a friend, but they are not friends.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.EntryGID));

            return;
        }

        // Check if the friend is online. If they are, we need to forward the drop to them. Otherwise,
        // we can just remove them from the database.
        if (TryGetOnlinePlayer(message.EntryGID, out var onlinePlayer)) {
            var fwdMsg = new CHARACTER_103_PROTOCOL.MSG_BUDDYDROPFWD {
                ListOwnerGID = wizard.CharId,
                EntryGID = message.EntryGID
            };
            Context.ActorSelection(onlinePlayer.ActorPath).Tell(fwdMsg);
        }
        else {
            var offlineWizard = WizardCollection.GetCharacter(message.EntryGID);
            offlineWizard.RemoveFriend(wizard.CharId);
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_BUDDYDROPFWD))]
    private void ReceiveBuddyDropFwd(CHARACTER_103_PROTOCOL.MSG_BUDDYDROPFWD message) {
        // A player has removed us as a friend. We need to remove them from our friends list.
        var wizard = GetActiveWizard();

        if (!wizard.RemoveFriend(message.EntryGID)) {
            Logger.Error("{0} tried to remove character ID {1} as a friend, but they are not friends.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.EntryGID));

            return;
        }

        // Inform the client that the friend has been removed.
        var clientMsg = new GAME_5_PROTOCOL.MSG_BUDDYDROP {
            ListOwnerGID = message.EntryGID,
            EntryGID = message.ListOwnerGID
        };
        SendToSocket(clientMsg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_GOTOPLAYER))]
    private void ReceiveGoToPlayer(GAME_5_PROTOCOL.MSG_GOTOPLAYER message) {
        var targetID = message.TargetCharacterID;

        // Check if the target is online.
        if (!TryGetOnlinePlayer(targetID, out var onlinePlayer)) {
            Logger.Debug("Player {0} tried to teleport to character ID {1}, but the character is not online.",
                Logger.Args(GetActiveWizard().PlayerNameBehavior.GetWizardName(), targetID));

            SendToSocket(new GAME_5_PROTOCOL.MSG_GOTOPLAYERRESP {
                Error = 1
            });

            return;
        }

        // Query for the target's Wizard so that we may get their X/Y/Z coordinates.
        var queryResult = Context.ActorSelection(onlinePlayer.ActorPath)
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(
                message: new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(),
                timeout: TimeSpan.FromSeconds(QUERY_TELEPORT_WIZARD_TIMEOUT_IN_SECONDS)
            )
            .Result;

        if (queryResult is not null) {
            var coordinates = Util.GetCompactStringFromVector((Vector4) queryResult.Wizard.Location);

            Teleport(
                destinationZone: onlinePlayer.CurrentZone,
                destinationLocation: coordinates,
                doTeleportEffects: true,
                ownerCharId: targetID
            );
        } else {
            Logger.Error("Failed to query the target wizard for teleportation.");
        }
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_IGNOREADD))]
    private void ReceiveIgnoreAdd(GAME_5_PROTOCOL.MSG_IGNOREADD message) {
        return; // TODO: Another time.

        // A player wants to ignore another player.
        var wizard = GetActiveWizard();

        if (!wizard.IgnorePlayer(message.CharacterGID)) {
            Logger.Error("{0} tried to ignore character ID {1}, but they are already ignored.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.CharacterGID));

            return;
        }
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_IGNOREDROP))]
    private void ReceiveIgnoreDrop(GAME_5_PROTOCOL.MSG_IGNOREDROP message) {
        // A player wants to unignore another player.
        var wizard = GetActiveWizard();

        if (!wizard.UnignorePlayer(message.CharacterGID)) {
            Logger.Error("{0} tried to unignore character ID {1}, but they are not ignored.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.CharacterGID));

            return;
        }
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_IGNORELIST))]
    private void ReceiveIgnoreList(GAME_5_PROTOCOL.MSG_IGNORELIST message) {
        // A player is requesting a list of all the players they have ignored.
        var wizard = GetActiveWizard();
        var ignoredRelationships = wizard.FriendsBehavior.GetIgnoredPlayers(wizard.CharId);

        // Serialize the ignored player list data.
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );

        if (!serializer.Serialize(ignoredRelationships, 1, out var ignoredListBytes)) {
            Logger.Error("Player {0} requested their ignore list, but the serialization failed.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName()));

            return;
        }

        var msg = new GAME_5_PROTOCOL.MSG_IGNORELIST {
            ListOwnerGID = wizard.GameObject?.m_globalID ?? wizard.CharId,
            ListData = ignoredListBytes
        };
        SendToSocket(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CLIENT_DISCONNECT))]
    private void ReceiveClientDisconnect()
        => InformBuddiesOfStatusChange(false);

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT))]
    private void ReceiveQueryLogout(GAME_5_PROTOCOL.MSG_QUERY_LOGOUT message)
        => InformBuddiesOfStatusChange(false);

    private void SendBuddyEntry(Wizard buddy, Relationship relationship, ulong ownerID) {
        // Check if this buddy is online.
        var isOnline = TryGetOnlinePlayer(buddy.CharId, out var onlinePlayer);
        var buddyHexName = buddy.PlayerNameBehavior.GetWizardNameAsByteHexString();
        var buddyByteName = DataManipulation.SpacedHexStringToBytes(buddyHexName);

        // Log
        var ownerName = GetActiveWizard().PlayerNameBehavior.GetWizardName();
        var statusMessage = isOnline ? "online" : "offline";
        var buddyWizardName = buddy.PlayerNameBehavior.GetWizardName();
        Logger.Debug("{0} has a friend named {1} (Hex: {2}) (ID: {3}) who is {4}.",
            Logger.Args(ownerName, buddyWizardName, buddyHexName, buddy.CharId, statusMessage));

        var buddyMsg = new GAME_5_PROTOCOL.MSG_BUDDYENTRY {
            ListOwnerGID = ownerID,
            EntryGID = buddy.CharId,
            GameObjectID = buddy.GameObject?.m_globalID ?? buddy.CharId,
            Name = buddyByteName,
            Status = isOnline ? ONLINE_STATUS_CODE : OFFLINE_STATUS_CODE,
            FriendInfo = 5702144,                                     // TODO: What is this?
            PasswordChat = 0,                                         // TODO: What is this?
            Permissions = (uint) buddy.Account.GetAccountFlags(),
            ZoneName = onlinePlayer?.CurrentZoneDisplayName ?? string.Empty,
            RealmName = onlinePlayer?.CurrentRealm ?? string.Empty,
            Locale = 0,
            FriendDate = relationship.RelationshipEpochInSeconds,
            FriendStatusDate = relationship.RelationshipEpochInSeconds,
            PreviousName = string.Empty                               // TODO: Implement this
        };
        SendToSocket(buddyMsg);
    }

    private void SendBuddyListEnd(ulong ownerID) {
        var completeMsg = new GAME_5_PROTOCOL.MSG_BUDDYLISTCOMPLETE {
            ListOwnerGID = ownerID
        };
        SendToSocket(completeMsg);
    }

    private void InformBuddiesOfStatusChange(bool isOnline) {
        var ownerID = GetActiveWizard().CharId;

        // Inform all buddies of the status change.
        var buddies = BuddyRelationshipCollection.GetBuddiesForWizard(ownerID);
        foreach (var buddy in buddies.Where(buddy => buddy != null)) {
            if (TryGetOnlinePlayer(buddy.CharId, out var onlinePlayer)) {
                var buddyStatusMsg = new GAME_5_PROTOCOL.MSG_BUDDYSTATUSUPDATE {
                    ListOwnerGID = buddy.CharId,
                    EntryGID = ownerID,
                    Status = isOnline ? ONLINE_STATUS_CODE : OFFLINE_STATUS_CODE,
                    ZoneName = onlinePlayer.CurrentZoneDisplayName,
                    RealmName = onlinePlayer.CurrentRealm
                };
                var buddyActorPath = onlinePlayer.ActorPath;
                Context.ActorSelection(buddyActorPath).Tell(buddyStatusMsg);
            }
        }
    }

    private WizGameStats GetBuddyGameStats(Wizard databaseBuddy) {
        // Dragon database doesn't store any game stats because they follow a consistent
        // pattern that allows us to recreate them when they log in. Since "database buddy"
        // doesn't have any stats applied, this function must recreate them as if they were
        // logging in themselves.
        CharacterHelper.RecalculateGameStats(databaseBuddy);

        // The client type alternative 'GetClientTypeAlternative' doesn't return applied
        // stats, only base stats. Use 'GetCombatGameStats' to get the base stats + applied.
        return databaseBuddy.GameStats.GetCombatGameStats();
    }

}
