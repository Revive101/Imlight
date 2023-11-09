/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Text;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.IO;
using Imlight.Common.Utilities;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Implementations;

namespace Imlight.CoreLib.Game.Services;

public class ChatService : MessageService {
    private const string FemaleSourcePrefix = "80";
    private const string MaleSourcePrefix = "82";

    public ChatService(SessionActor sessionActor) : base(sessionActor) {
    }

    protected static Props Props(SessionActor parentActor) {
        return Akka.Actor.Props.Create(() => new ChatService(parentActor));
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT))]
    private void ReceiveRequestRadialChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT message) {
        var globalId = GetActiveCoreObject().m_globalID;
        var character = GetActiveCharacter();
        var nameIndices = character.NameIndices;
        var gender = character.WizardAvatar.m_eGender;
        var src = CraftSourceNameFromIndices(nameIndices, gender);

        // Remove the '\f', '\n', '\0' character from the message.
        var cleanedMessage = message.Message.ToString()?.Replace(@"\f", "")?.Replace("\n", "")?.Replace("\0", "")[1..];

        // Parse in-game chat commands. Do not broadcast it to the zone.
        if (cleanedMessage.StartsWith('/')) {
            Logger.Information($"{SessionActor.SessionID} used QA command: {cleanedMessage}");
            ParseQAChatCommand(cleanedMessage, character);
            return;
        }

        Logger.Information($"{SessionActor.SessionID} says in chat: {cleanedMessage}");

        // Broadcast the message to the zone.
        var msg = new GAME_5_PROTOCOL.MSG_RADIALCHAT {
            Message = message.Message,
            SourceID = globalId,
            SourceName = src,
            Filter = 0
        };
        ZoneBroadcast(msg);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
    private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message) {
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

    private static ByteString GetMessagePayload(byte[] input) {
        // The message is a wide string.
        var msgBuffer = new BitReader(input);
        var msgSize = msgBuffer.ReadUInt16() * 2; // Account for unicode
        var msgTextRaw = msgBuffer.ReadBytes(msgSize);
        var msgText = Encoding.Unicode.GetString(msgTextRaw);

        return msgText;
    }

    private static ByteString CraftMessagePayload(string input) {
        // Convert input string to byte array using Unicode encoding
        var newTextBytes = Encoding.Unicode.GetBytes(input);
        var rebuffer = new BitWriter();

        // Calculate length of byte array, rounded up to nearest multiple of 2
        var len = (ushort) ((newTextBytes.Length + 2) / 2);
        rebuffer.WriteUInt16(len);
        rebuffer.WriteBytes(newTextBytes);
        rebuffer.WriteUInt16(32);

        return new ByteString(rebuffer.GetData());
    }

    private static byte[] CraftSourceNameFromIndices(uint input, TypeCache.eGender gender) {
        // Drop the MSB from input, then convert it to a hex string.
        var raw = (input & 0x7FFFFFFF).ToString("X8");
        var sb = new StringBuilder(raw);
        for (int i = sb.Length - 2; i >= 0; i -= 2)
            sb.Insert(i, ' ');
        var tail = sb.ToString().TrimStart();

        // Replace the first 2 characters depending on gender.
        var newMsb = gender == TypeCache.eGender.Female ? FemaleSourcePrefix : MaleSourcePrefix;
        tail = newMsb + tail.Substring(2);

        return DataManipulation.SpacedHexStringToBytes(tail);
    }

    private void ParseQAChatCommand(string input, Character character) {
        // Get account from character and check authorization level.
        var account = AccountCollection.GetAccount(character.AccountId);
        if (account.AuthLevel < AuthLevel.Administrator)
            return;

        // Parse the command.
        var command = input.Split(' ');
        switch (command[0]) {
            case "/port":
                var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
                    DestinationZone = command[1],
                    DestinationLocation = "Start",
                    SendToClient = true
                };
                TellOtherServices(msg);
                break;
            default:
                Logger.Information($"Unknown QA command: {command[0]}");
                break;
        }
    }
}
