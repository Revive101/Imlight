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
 * Last Updated: 10/25/2026
 */

using System;
using System.IO;
using System.Linq;
using Akka.Actor;
using Imcodec.IO;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.Game.Groups;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class ChatService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const string COMMAND_PREFIX = ".";

    private Wizard _selectedCharacter;
    private Account _selectedAccount;

    private readonly IActorRef _dispatcherRef = CommandDispatcher.Instance;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new ChatService(parentActor));

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT))]
    private void ReceiveRequestRadialChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALCHAT message) {
        if (IsSenderMuted()) {
            return;
        }

        var charObj = GetActiveGameObject();
        var wizard = GetActiveWizard();
        var account = GetActiveAccount();

        var cleanedMessage = DecodeRadialChatText(message.Message);
        if (cleanedMessage.Length == 0) {
            return;
        }

        // Commands are never broadcast to the zone.
        if (cleanedMessage.StartsWith(COMMAND_PREFIX) && account.AuthLevel > AuthLevel.None) {
            SendChatCommand(cleanedMessage, charObj, wizard);

            return;
        }

        LogChatMessage(wizard.PlayerNameBehavior.GetWizardName(), cleanedMessage, wizard.Zone);
        SaveChatLog(cleanedMessage, wizard);

        var msg = new GAME_5_PROTOCOL.MSG_RADIALCHAT {
            Message = EncodeRadialChatText(cleanedMessage),
            SourceID = charObj.m_globalID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Filter = 2,
        };
        SendFilteredZoneMessage(msg, wizard.CharId, wizard.Zone);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT))]
    private void ReceiveRequestRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTRADIALQUICKCHAT message) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        var msg = new GAME_5_PROTOCOL.MSG_RADIALQUICKCHAT() {
            MessageID = message.MessageID,
            SourceID = GetActiveGameObject().m_globalID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Filter = 0,
        };
        SendFilteredZoneMessage(msg, wizard.CharId, wizard.Zone);
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHAT))]
    private void ReceiveWhisperRadialQuickChat(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHAT message) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        SendDirectedChat(message.TargetID, new GAME_5_PROTOCOL.MSG_DIRECTEDQUICKCHAT {
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            SourceID = wizard.CharId,
            MessageID = message.MessageID,
            Filter = 0
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHATEXT))]
    private void ReceiveWhisperRadialChatExt(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDQUICKCHATEXT message) {
        if (IsSenderMuted()) {
            return;
        }

        var wizard = GetActiveWizard();
        SendDirectedChat(message.TargetID, new GAME_5_PROTOCOL.MSG_DIRECTEDQUICKCHATEXT {
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            SourceID = wizard.CharId,
            Message = message.Message,
            Filter = 0
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHAT))]
    private void ReceiveWhisperDirectChat(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHAT message) {
        if (IsSenderMuted()) {
            return;
        }

        // The client's /party line is a directed chat aimed at the group's channel ID.
        var wizard = GetActiveWizard();
        if (GroupRegistry.IsGroupChannel(message.TargetID)) {
            SendGroupChat(message, wizard);

            return;
        }

        SendDirectedChat(message.TargetID, new GAME_5_PROTOCOL.MSG_DIRECTEDCHAT {
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            SourceID = wizard.CharId,
            Message = message.Message,
            Filter = 0
        });
    }

    [MessageHandler(typeof(GAME_5_PROTOCOL.MSG_BUDDYSTATS))]
    private void ReceivePlayerSelect(GAME_5_PROTOCOL.MSG_BUDDYSTATS message) {
        var localAccount = GetActiveAccount();
        if (localAccount.AuthLevel < AuthLevel.HallMonitor) {
            return;
        }

        if (message.BuddyID == 0) {
            _selectedCharacter = null;
            _selectedAccount = null;

            return;
        }
        
        if (!Wizard.TryGetCharacterId(message.BuddyID, out var characterId)) {
            return;
        }

        var persistentCharacter = WizardCollection.GetCharacter(characterId);
        if (persistentCharacter is null) {
            return;
        }

        var selectedAccount = AccountCollection.GetAccount(persistentCharacter.AccountId);
        if (selectedAccount is null) {
            return;
        }

        _selectedCharacter = persistentCharacter;
        _selectedAccount = selectedAccount;
    }

    private bool IsSenderMuted() {
        if (!GetActiveAccount().InfractionHistory.IsCurrentlyMuted) {
            return false;
        }

        InformGameClient("You are currently muted.");

        return true;
    }

    private void SendDirectedChat(ulong targetId, IMessage msg) {
        if (BuddyRelationshipCollection.HasBlocked(targetId, GetActiveWizard().CharId)) {
            return;
        }

        if (!TryGetOnlinePlayer(targetId, out var targetPlayer)) {
            SendToSocket(new GAME_5_PROTOCOL.MSG_DIRECTEDCHATFAIL());

            return;
        }

        Context.ActorSelection(targetPlayer.ActorPath).Tell(msg);
    }

    private static string DecodeRadialChatText(ByteString payload) {
        if (payload.IsEmpty) {
            return string.Empty;
        }

        try {
            var lengthReader = new BitReader((byte[])payload);
            var characterCount = lengthReader.ReadUInt16();
            if (lengthReader.GetRelativeData().Length < characterCount * 2) {
                return string.Empty;
            }

            var text = new BitReader((byte[])payload).ReadWString();

            return new string(text.Where(character => !char.IsControl(character)).ToArray()).Trim();
        }
        catch (EndOfStreamException) {
            return string.Empty;
        }
    }

    private static ByteString EncodeRadialChatText(string text) {
        var writer = new BitWriter();
        writer.WriteWString(text);

        return new ByteString(writer.GetData());
    }

    private void SendGroupChat(GAME_5_PROTOCOL.MSG_REQUESTDIRECTEDCHAT message, Wizard wizard) {
        var text = message.Message.ToString() ?? string.Empty;

        GroupDirectory.Instance?.Tell(new GROUP_109_PROTOCOL.MSG_GROUPCHAT {
            SenderCharId = wizard.CharId,
            ChannelId = message.TargetID,
            SourceName = wizard.PlayerNameBehavior.GetPackedNameBytes(),
            Message = message.Message,
            CharIdsIgnoringSender = BuddyRelationshipCollection.GetCharactersWhoBlocked(wizard.CharId)
        });

        Logger.Information("[Group {Channel}] {Name}: {Message}",
            Logger.Args(message.TargetID, wizard.PlayerNameBehavior.GetWizardName(), text));
        SaveChatLog(text, wizard);
    }

    private static void LogChatMessage(string name, string message, string zoneName)
        => Logger.Information("[{0}] {1}: {2}", Logger.Args(zoneName, name, message));

    private static void SaveChatLog(string message, Wizard wizard)
        => ChatLogCollection.AddChatLog(new ChatLog() {
            TimeStamp = DateTime.UtcNow,
            ZoneName = wizard.Zone,
            CharacterId = wizard.CharId,
            AccountId = wizard.AccountId,
            Message = message,
        });

    private void SendFilteredZoneMessage(IMessage msg, ulong senderCharId, string zoneName) {
        // Players who ignored the sender do not get the message.
        var blockedBy = BuddyRelationshipCollection.GetCharactersWhoBlocked(senderCharId);
        var playersInZone = OnlinePlayerCollection.GetPlayersInZone(zoneName);

        foreach (var player in playersInZone) {
            // The sender's client shows their own message already.
            if (player.CharacterId == senderCharId) {
                continue;
            }

            if (blockedBy.Contains(player.CharacterId)) {
                continue;
            }

            Context.ActorSelection(player.ActorPath).Tell(msg);
        }
    }

    private void SendChatCommand(string input, CoreObject charObj, Wizard wizard) {
        var account = GetActiveAccount();

        _dispatcherRef.Tell(new SERVER_100_PROTOCOL.MSG_COMMAND() {
            CommandText = input[COMMAND_PREFIX.Length..],
            ActorRef = SessionActor.ActorRef,
            CoreObject = charObj,
            Wizard = wizard,
            Account = account,
            ZoneActor = SessionActor.GetZoneActor(),
            ServerActor = SessionActor.ServerRef,
            SelectedWizard = _selectedCharacter,
            SelectedAccount = _selectedAccount
        });
    }

}