/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Text;
using System.Text.RegularExpressions;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.Utilities;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.WizardData.Models;

namespace Imlight.CoreLib.Game.Services;

public class ChatService : MessageService {
    private const string FemaleSourcePrefix = "80";
    private const string MaleSourcePrefix = "82";
    private const char CommandPrefix = '.';

    private Character _selectedCharacter;
    private Account _selectedAccount;

    private IActorRef _dispatcherRef;

    public ChatService(SessionActor sessionActor) : base(sessionActor) { _dispatcherRef = CommandDispatcher.Instance; }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new ChatService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT))]
    private void ReceiveRequestRadialChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT message) {
        var charObj = GetActiveCoreObject();
        var character = GetActiveCharacter();
        var account = GetActiveAccount();

        if (account.InfractionHistory.IsCurrentlyMuted) {
            InformGameClient("You are currently muted.");
            return;
        }

        // Craft the wizard name.
        var nameIndices = character.NameIndices;
        var gender = character.WizardAvatar.m_eGender;
        var sourceName = CraftSourceNameFromIndices(nameIndices, gender);

        var cleanedMessage = CleanMessage(message.Message);

        // Parse in-game chat commands. Do not broadcast it to the zone.
        if (cleanedMessage.StartsWith(CommandPrefix) && account.AuthLevel > AuthLevel.None) {
            // Remove the commandp prefix.
            SendChatCommand(cleanedMessage);
            return;
        }

        Logger.Information($"{SessionActor.SessionID} says in chat: {cleanedMessage}");

        // Add the chat log to the database.
        var chatLog = new ChatLog() {
            TimeStamp = DateTime.UtcNow,
            ZoneName = character.Zone,
            CharacterId = charObj.m_globalID,
            AccountId = character.AccountId,
            Message = cleanedMessage,
        };
        ChatLogCollection.AddChatLog(chatLog);

        // Broadcast the message to the zone.
        var msg = new GAME_5_PROTOCOL.MSG_RADIALCHAT {
            Message = message.Message,
            SourceID = charObj.m_globalID,
            SourceName = sourceName,
            Filter = 0
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

        var globalId = GetActiveCoreObject().m_globalID;
        var character = GetActiveCharacter();
        var nameIndices = character.NameIndices;
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

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQASKSERVER))]
    private void ReceiveRequest(GAME_5_PROTOCOL.MSG_REQASKSERVER message) {

    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_CORE_EMOTE))]
    private void ReceiveCoreEmote(GAME_5_PROTOCOL.MSG_CORE_EMOTE message) {
        // todo
        TellOtherServices(new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST() {
            Sender = SessionActor.ActorRef,
            Message = message,
            Selfless = true,
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYSTATS))]
    private void ReceivePlayerSelect(GAME_5_PROTOCOL.MSG_BUDDYSTATS message) {
        // We only care about the ID sent here. It's the ID of the core object, but Imlight serialized
        // it using the character ID.
        var id = message.BuddyID;

        var persistentCharacter = CharacterCollection.GetCharacter(id);
        if (persistentCharacter is null) {
            return;
        }

        var account = AccountCollection.GetAccount(persistentCharacter.AccountId);
        if (account is null) {
            return;
        }

        // Cache for our next command.
        _selectedCharacter = persistentCharacter;
        _selectedAccount = account;
    }

    private static string CleanMessage(ByteString message) {
        if (message == null) {
            return null;
        }

        // Define a regular expression pattern to keep alphanumeric characters and punctuation
        string validCharactersPattern = "[^a-zA-Z0-9\\p{P} ]"; // \p{P} matches any punctuation character
        var cleanedMessage = Regex.Replace(message.ToString(), validCharactersPattern, "").Trim();

        return cleanedMessage;
    }

    private static byte[] CraftSourceNameFromIndices(uint input, TypeCache.eGender gender) {
        // Drop the MSB from input, then convert it to a hex string.
        var raw = (input & 0x7FFFFFFF).ToString("X8");
        var sb = new StringBuilder(raw);
        for (int i = sb.Length - 2; i >= 0; i -= 2) {
            sb.Insert(i, ' ');
        }

        var tail = sb.ToString().TrimStart();

        // Replace the first 2 characters depending on gender.
        var newMsb = gender == TypeCache.eGender.Female ? FemaleSourcePrefix : MaleSourcePrefix;
        tail = newMsb + tail[2..];

        return DataManipulation.SpacedHexStringToBytes(tail);
    }

    private void SendChatCommand(string input) {
        var charObj = GetActiveCoreObject();
        var character = GetActiveCharacter();
        var account = GetActiveAccount();

        _dispatcherRef.Tell(new SERVER_100_PROTOCOL.MSG_COMMAND() {
            CommandText = input[1..], // Remove the command prefix
            ActorRef = SessionActor.ActorRef,
            CoreObject = charObj,
            PlayerCharacter = character,
            Account = account,
            ZoneActor = SessionActor.GetZoneActor(),
            ServerActor = SessionActor.ServerRef,
            SelectedCharacter = _selectedCharacter,
            SelectedAccount = _selectedAccount
        });
    }
}
