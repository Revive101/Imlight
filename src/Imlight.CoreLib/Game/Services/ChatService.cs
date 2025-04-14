/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
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
using System.Text;
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
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

public class ChatService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const string FemaleSourcePrefix = "80";
    private const string MaleSourcePrefix = "82";

    // Always make sure this command prefix is within the bounds of the Regex.
    private const string CommandPrefix = ".";
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

        // Craft the wizard name.*
        var nameIndices = wizard.PlayerNameBehavior.NameIndices;
        var gender = wizard.WizardAvatar.m_eGender;
        var sourceName = CraftSourceNameFromIndices(nameIndices, gender);
        var cleanedMessage = CleanMessageTrash(message.Message);

        // Parse in-game chat commands. Do not broadcast it to the zone.
        if (cleanedMessage.StartsWith(CommandPrefix) && account.AuthLevel > AuthLevel.None) {
            SendChatCommand(cleanedMessage);
            return;
        }

        if (account.InfractionHistory.IsCurrentlyMuted) {
            InformGameClient("You are currently muted.");
            return;
        }

        // Log the chat message both to console and database.
        var wizardName = wizard.PlayerNameBehavior.GetWizardName();
        var zoneName = wizard.ZoneDisplayName;
        LogChatMessage(wizardName, cleanedMessage, zoneName);
        SaveChatLog(cleanedMessage);

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
        var nameIndices = character.PlayerNameBehavior.NameIndices;
        var gender = character.WizardAvatar.m_eGender;
        var src = CraftSourceNameFromIndices(nameIndices, gender);

        var msg = new GAME_5_PROTOCOL.MSG_RADIALQUICKCHAT() {
            MessageID = message.MessageID,
            SourceID = globalId,
            SourceName = src,
            Filter = 0,
        };
        ZoneBroadcast(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYSTATS))]
    private void ReceivePlayerSelect(GAME_5_PROTOCOL.MSG_BUDDYSTATS message) {
        var account = GetActiveAccount();
        if (account.AuthLevel < AuthLevel.HallMonitor) {
            return;
        }

        // We only care about the ID sent here. It's the ID of the core object, but Imlight serialized
        // it using the character ID.
        var id = message.BuddyID;

        var persistentCharacter = WizardCollection.GetCharacter(id);
        if (persistentCharacter is null) {
            return;
        }

        var selectedAccount = AccountCollection.GetAccount(persistentCharacter.AccountId);
        if (selectedAccount is null) {
            return;
        }

        // Cache for our next command.
        _selectedCharacter = persistentCharacter;
        _selectedAccount = account;
    }

    private static string CleanMessageTrash(ByteString message) {
        var sMessage = message.ToString();
        if (sMessage == null) {
            return null;
        }

        // Remove the first byte, unless it's the command prefix.
        if (!message.ToString().StartsWith(CommandPrefix)) {
            message = ((byte[])message)[1..];
        }

        // Remove any non-printable characters.
        var cleanedMessage = Regex.Replace(message.ToString(), @"[^\u0020-\u007E]", string.Empty);

        return cleanedMessage;
    }

    private static ByteString CraftSourceNameFromIndices(uint input, eGender gender) {
        // Drop the MSB from input, then convert it to a hex string.
        var raw = (input & 0x7FFFFFFF).ToString("X8");
        var sb = new StringBuilder(raw);
        for (int i = sb.Length - 2; i >= 0; i -= 2) {
            sb.Insert(i, ' ');
        }

        var tail = sb.ToString().TrimStart();

        // Replace the first 2 characters depending on gender.
        var newMsb = gender == eGender.Female ? FemaleSourcePrefix : MaleSourcePrefix;
        tail = newMsb + tail[2..];

        return DataManipulation.SpacedHexStringToBytes(tail);
    }

    private void SendChatCommand(string input) {
        var charObj = GetActiveGameObject();
        var character = GetActiveWizard();
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

    private void SaveChatLog(string message) {
        var charObj = GetActiveGameObject();
        var character = GetActiveWizard();

        var chatLog = new ChatLog() {
            TimeStamp = DateTime.UtcNow,
            ZoneName = character.Zone,
            CharacterId = charObj.m_globalID,
            AccountId = character.AccountId,
            Message = message,
        };
        ChatLogCollection.AddChatLog(chatLog);
    }

    private static void LogChatMessage(string name, string message, string zoneName) 
        => Logger.Information("[{0}] {1}: {2}", Logger.Args(zoneName, name, message));
        
}
