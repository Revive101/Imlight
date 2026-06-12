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
 * CHAT SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages in-game chat functionality, including message processing, 
 * command handling, and logging of player communications.
 * 
 * USAGE EXAMPLE:
 * Internal service handling chat-related messages within the game server's 
 * session management system.
 * 
 * NOTE:
 * - Implements chat command processing for authorized users
 * 
 * TODO:
 * - Review and enhance chat command authorization
 * - Improve message sanitization logic
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Text.RegularExpressions;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class ChatService(SessionActor sessionActor) : MessageService(sessionActor) {

    // Always make sure this command prefix is within the bounds of the Regex.
    private const string CommandPrefix = ".";
    private const string MessageRegex = "[^a-zA-Z0-9\\p{P} ]";

    private Wizard _selectedCharacter;
    private Account _selectedAccount;

    private readonly IActorRef _dispatcherRef = CommandDispatcher.Instance;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new ChatService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT))]
    private void ReceiveRequestRadialChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT message) {
        var charObj = GetActiveGameObject();
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();

        if (account.InfractionHistory.IsCurrentlyMuted) {
            InformGameClient("You are currently muted.");

            return;
        }

        // Craft the wizard name.
        var byteName = wizard.PlayerNameBehavior.GetWizardNameAsByteHexString();
        var sourceName = DataManipulation.SpacedHexStringToBytes(byteName);

        var cleanedMessage = CleanMessageTrash(message.Message);

        // Parse in-game chat commands. Do not broadcast it to the zone.
        if (cleanedMessage.StartsWith(CommandPrefix) && account.AuthLevel > AuthLevel.None) {
            SendChatCommand(cleanedMessage, charObj, wizard);

            return;
        }

        LogChatMessage(wizard.PlayerNameBehavior.GetWizardName(), cleanedMessage, wizard.Zone);
        SaveChatLog(cleanedMessage, charObj, wizard);

        // Broadcast the message to the zone.
        var msg = new GAME_5_PROTOCOL.MSG_RADIALCHAT {
            Message = message.Message,
            SourceID = charObj.m_globalID,
            SourceName = sourceName,
            Filter = 2,
        };
        ZoneBroadcast(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
    private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message) {
        var account = GetActiveAccount();
        if (account.InfractionHistory.IsCurrentlyMuted) {
            InformGameClient("You are currently muted.");

            return;
        }

        var globalId = GetActiveGameObject().m_globalID;
        var character = GetActiveWizard();
        var byteName = character.PlayerNameBehavior.GetWizardNameAsByteHexString();
        var src = DataManipulation.SpacedHexStringToBytes(byteName);

        var msg = new GAME_5_PROTOCOL.MSG_RADIALQUICKCHAT() {
            MessageID = message.MessageID,
            SourceID = globalId,
            SourceName = src,
            Filter = 0,
        };
        ZoneBroadcast(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHAT))]
    private void ReceiveWhisperRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHAT message) {
        // A player is whispering to another player using the radial quick chat.
        var targetID = message.TargetID;

        // Check if the sender is muted.
        var account = GetActiveAccount();
        if (account.InfractionHistory.IsCurrentlyMuted) {
            InformGameClient("You are currently muted.");

            return;
        }
        
        if (!TryGetOnlinePlayer(targetID, out var targetPlayer)) {
            // Inform the user of error if the target is offline.
            SendToSocket(new GAME_5_PROTOCOL.MSG_DIRECTEDCHATFAIL());

            return;
        }

        // Send the quick chat to the target player.
        var myWizard = GetActiveWizard();
        var hexName = myWizard.PlayerNameBehavior.GetWizardNameAsByteHexString();
        var sourceName = DataManipulation.SpacedHexStringToBytes(hexName);
        var msg = new GAME_5_PROTOCOL.MSG_DIRECTEDQUICKCHAT {
            SourceName = sourceName,
            SourceID = myWizard.CharId,
            MessageID = message.MessageID,
            Filter = 0
        };

        var targetActorPath = targetPlayer.ActorPath;
        Context.ActorSelection(targetActorPath).Tell(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHATEXT))]
    private void ReceiveWhisperRadialChatExt(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHATEXT message) {
        // A player is whispering to another player, directly.
        // This message uses UTF-8 encoding.
        var targetID = message.TargetID;

        // Check if the sender is muted.
        var account = GetActiveAccount();
        if (account.InfractionHistory.IsCurrentlyMuted) {
            InformGameClient("You are currently muted.");

            return;
        }

        if (!TryGetOnlinePlayer(targetID, out var targetPlayer)) {
            // Inform the user of error if the target is offline.
            SendToSocket(new GAME_5_PROTOCOL.MSG_DIRECTEDCHATFAIL());

            return;
        }

        // Send the directed chat to the target player.
        var myWizard = GetActiveWizard();
        var hexName = myWizard.PlayerNameBehavior.GetWizardNameAsByteHexString();
        var sourceName = DataManipulation.SpacedHexStringToBytes(hexName);
        var msg = new GAME_5_PROTOCOL.MSG_DIRECTEDQUICKCHATEXT {
            SourceName = sourceName,
            SourceID = myWizard.CharId,
            Message = message.Message,
            Filter = 0
        };

        var targetActorPath = targetPlayer.ActorPath;
        Context.ActorSelection(targetActorPath).Tell(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHAT))]
    private void ReceiveWhisperDirectChat(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHAT message) {
        // A player is whispering to another player, directly.
        // This message uses a wide-character string (16 bits per character).
        var targetID = message.TargetID;

        // Check if the sender is muted.
        var account = GetActiveAccount();
        if (account.InfractionHistory.IsCurrentlyMuted) {
            InformGameClient("You are currently muted.");

            return;
        }

        if (!TryGetOnlinePlayer(targetID, out var targetPlayer)) {
            // Inform the user of error if the target is offline.
            SendToSocket(new GAME_5_PROTOCOL.MSG_DIRECTEDCHATFAIL());

            return;
        }

        // Send the directed chat to the target player.
        var myWizard = GetActiveWizard();
        var hexName = myWizard.PlayerNameBehavior.GetWizardNameAsByteHexString();
        var sourceName = DataManipulation.SpacedHexStringToBytes(hexName);
        var msg = new GAME_5_PROTOCOL.MSG_DIRECTEDCHAT {
            SourceName = sourceName,
            SourceID = myWizard.CharId,
            Message = message.Message,
            Filter = 0
        };

        var targetActorPath = targetPlayer.ActorPath;
        Context.ActorSelection(targetActorPath).Tell(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYSTATS))]
    private void ReceivePlayerSelect(GAME_5_PROTOCOL.MSG_BUDDYSTATS message) {
        // Regular players have no reason to be able to select other players
        // for command context.
        var localAccount = GetActiveAccount();
        if (localAccount.AuthLevel < AuthLevel.HallMonitor) {
            return;
        }

        if (message.BuddyID == 0) {
            _selectedCharacter = null;
            _selectedAccount = null;

            return;
        }
        
        // We only care about the ID sent here. It's the ID of the core object, but Imlight serialized
        // it using the character ID.
        var persistentCharacter = WizardCollection.GetCharacter(message.BuddyID);
        if (persistentCharacter is null) {
            return;
        }

        var selectedAccount = AccountCollection.GetAccount(persistentCharacter.AccountId);
        if (selectedAccount is null) {
            return;
        }

        // Cache for our next command.
        _selectedCharacter = persistentCharacter;
        _selectedAccount = selectedAccount;
    }

    private static string CleanMessageTrash(ByteString message) {
        if (message == null) {
            return null;
        }

        // Remove the first byte, unless it's the command prefix.
        if (!message.ToString().StartsWith(CommandPrefix)) {
            message = ((byte[])message)[1..];
        }

        // Define a regular expression pattern to keep alphanumeric characters and punctuation
        string validCharactersPattern = MessageRegex; // \p{P} matches any punctuation character
        var cleanedMessage = Regex.Replace(message.ToString(), validCharactersPattern, "").Trim();

        return cleanedMessage;
    }

    private static void LogChatMessage(string name, string message, string zoneName) 
        => Logger.Information("[{0}] {1}: {2}", Logger.Args(zoneName, name, message));

    private static void SaveChatLog(string message, CoreObject charObj, Wizard character) 
        => ChatLogCollection.AddChatLog(new ChatLog() {
            TimeStamp = DateTime.UtcNow,
            ZoneName = character.Zone,
            CharacterId = charObj.m_globalID,
            AccountId = character.AccountId,
            Message = message,
        });

    private void SendChatCommand(string input, CoreObject charObj, Wizard character) {
        var account = GetActiveAccount();

        _dispatcherRef.Tell(new SERVER_100_PROTOCOL.MSG_COMMAND() {
            CommandText = input[1..], // Remove the command prefix
            ActorRef = SessionActor.ActorRef,
            CoreObject = charObj,
            Wizard = character,
            Account = account,
            ZoneActor = SessionActor.GetZoneActor(),
            ServerActor = SessionActor.ServerRef,
            SelectedWizard = _selectedCharacter,
            SelectedAccount = _selectedAccount
        });
    }

}