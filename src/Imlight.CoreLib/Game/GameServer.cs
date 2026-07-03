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
 * GAME SERVER 
 * ========================================================================
 * 
 * PURPOSE:
 * Handles game sessions, player queuing, authentication via session keys, 
 * and coordinates between game world, command processing, and process supervision.
 * 
 * USAGE EXAMPLE:
 * var gameServer = system.ActorOf(GameServer.Props("World1", 6001), "gameServer");
 * 
 * NOTE:
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.Common;
using Imlight.CoreLib.Game.Commands;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Game.Processes;
using Imlight.CoreLib.Shared.Cryptography;
using Imlight.CoreLib.Shared.Structures;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game;

public class GameServer : Server {

    private readonly string _sessionKeyHashInput = ConfigurationManager.Settings["Advanced.SessionKeyHashInput"].AsString();
    private readonly ushort _sessionKeyValidityTime = ConfigurationManager.Settings["Game Server.SessionKeyValidityTime"].AsUShort();
    private readonly ushort _playerLimit = ConfigurationManager.Settings["Game Server.GameServerPlayerLimit"].AsUShort();

    private readonly IActorRef _gameWorldRef;
    private readonly IActorRef _commandDispatcherRef;
    private readonly IActorRef _processSupervisorRef;
    private readonly Cache<ByteString, ulong> _sessionKeys;
    private readonly ListQueue<SessionActor> _playerQueue;
    private readonly ConcurrentDictionary<string, FallbackEntry> _fallbackEntries = [];

    public GameServer(string serverName, ushort serverPort, string realmName = null)
        : base(serverName, serverPort, GameServiceFactory.Props(),
              ConfigurationManager.Settings["Game Server.GameServerIP"].AsString()) {
        RealmName = realmName ?? serverName;
        this._playerQueue = new ListQueue<SessionActor>();
        this._sessionKeys = new Cache<ByteString, ulong>();
        this.ActiveSessions.CollectionChanged += ActiveSessionsChangedEvent;

        // Create actor children.
        var gameWorldActorName = $"{Name}.{nameof(GameWorld)}";
        _gameWorldRef = Context.ActorOf(GameWorld.Props(this), gameWorldActorName);
        Logger.Verbose("New actor created under {Path}: {Name}",
            Logger.Args(Context.Self.Path, gameWorldActorName));

        var commandDispatcherActorName = $"{Name}.{nameof(CommandDispatcher)}";
        _commandDispatcherRef = Context.ActorOf(CommandDispatcher.Props(), commandDispatcherActorName);
        Logger.Verbose("New actor created under {Path}: {Name}",
            Logger.Args(Context.Self.Path, commandDispatcherActorName));

        var processSupervisorActorName = $"{Name}.{nameof(ProcessSupervisor)}";
        _processSupervisorRef = Context.ActorOf(ProcessSupervisor.Props(), processSupervisorActorName);
        Logger.Verbose("New actor created under {Path}: {Name}",
            Logger.Args(Context.Self.Path, processSupervisorActorName));

        LoadResources();

        // Log
        Logger.Information("Game server created with name {Name} under port {Port}.",
            Logger.Args(serverName, serverPort));
    }

    public static Props Props(string serverName, ushort serverPort, string realmName = null)
        => Akka.Actor.Props.Create(() => new GameServer(serverName, serverPort, realmName));

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEKEY))]
    private void ReceiveCreateKey(SERVER_100_PROTOCOL.MSG_CREATEKEY message) {
        var key = CreateKey(message.Account.AccountId);

        var rsp = new SERVER_100_PROTOCOL.MSG_CREATEKEYRSP() { Key = key };
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEY))]
    private void ReceiveCreatePlayerKey(SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEY message) {
        // If this server is the target realm, create the key locally.
        if (message.TargetRealmName == RealmName) {
            var key = CreateKey(message.Account.AccountId);

            Sender.Tell(new SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEYRSP {
                Key = key,
                IP = Ip,
                Port = (ushort) Port,
                RealmName = RealmName,
                Success = true
            });

            return;
        }

        // Otherwise, forward to the GameServerPool to route to the correct realm.
        Context.Parent.Forward(message);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_REALMLIST))]
    private void ReceiveRealmList(SERVER_100_PROTOCOL.MSG_REALMLIST message) {
        // Forward to GameServerPool — it knows about all realms.
        Context.Parent.Forward(message);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYREALMSERVER))]
    private void ReceiveQueryRealmServer(SERVER_100_PROTOCOL.MSG_QUERYREALMSERVER message) {
        // Forward to GameServerPool — it knows about all realms.
        Context.Parent.Forward(message);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY))]
    private void ReceiveValidateSessionKey(SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY message) {
        // A user has requested to join this server. We're going to check if the session key is valid.
        // If it is, we'll return the account associated with it. If not, we'll return an error code.
        var keyTest = SessionKey.GenerateHash(_sessionKeyHashInput, message.UserID);

        foreach (var cachedKey in _sessionKeys) {
            if (keyTest != cachedKey.Key) {
                continue;
            }

            ActiveSessions.Add(message.SessionActor);

            // Get the account associated with this key.
            var accountId = cachedKey.Value;
            var account = AccountCollection.GetAccount(accountId);

            // Inform the client that the session key is valid. We'll also send the account associated with it.
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP() {
                ErrorCode = account is null ? 1 : 0,
                Account = account
            });

            return;
        }

        // The session key was not found in the cache. Return an error code.
        Sender.Tell(new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP() { ErrorCode = 1 });
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED))]
    private void ReceivePlayerEnqueued(SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED message) {
        // A player has requested to join this server.
        var rsp = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED() {
            PrepPhase = 0,
            Slot = 0
        };

        // If this is a VIP, we'll let them in immediately.
        if (message.VIPEntry) {
            Sender.Tell(rsp);
            return;
        }

        // If the server is full, add them to the queue and inform the client.
        if (ActiveSessions.Count >= _playerLimit) {
            _playerQueue.Enqueue(message.SessionActor);
            var queuePos = _playerQueue.Count;

            message.SessionActor.PlaceInQueue((ushort) queuePos);

            rsp.PrepPhase = 1;
            rsp.Slot = queuePos;
        }

        // Only a session that exists on the login server will even bother trying to enqueue itself.
        // Meaning that we don't actually want to add it to the active sessions here.
        Sender.Tell(rsp);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_KICKPLAYER))]
    private void ReceiveKickPlayer(SERVER_100_PROTOCOL.MSG_KICKPLAYER message) {
        // A player has requested to be kicked from the server.
        var session = ActiveSessions.FirstOrDefault(s => s.GetAssociatedAccount()?.AccountId == message.AccountID);
        if (session is null) {
            return;
        }

        var kickedMsg = new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE {
            Message = "You have been kicked from the server.",
            Modal = 1
        };
        session.ActorRef.Tell(kickedMsg);

        session.Dispose();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        _gameWorldRef.Forward(message);
    }

    [MessageHandler(typeof(PROCESS_107_PROTOCOL.MSG_NEW_MINIGAME_PROCESS))]
    private void ReceiveNewProcess(PROCESS_107_PROTOCOL.MSG_NEW_MINIGAME_PROCESS message) 
        => _processSupervisorRef.Forward(message);

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_REGISTER_FALLBACK))]
    private void ReceiveRegisterFallback(SERVICE_101_PROTOCOL.MSG_REGISTER_FALLBACK message) {
        _fallbackEntries[message.RemoteIp] = new FallbackEntry {
            UserId = message.UserId,
            CharId = message.CharId,
            FallbackZone = message.FallbackZone,
            FallbackZoneId = message.FallbackZoneId,
            FallbackLocation = message.FallbackLocation,
            GameServerIp = message.GameServerIp,
            GameServerPort = message.GameServerPort,
            RegisteredAt = DateTime.UtcNow
        };

        Logger.Debug("Fallback registered for IP {RemoteIp} → zone {Zone}",
            Logger.Args(message.RemoteIp, message.FallbackZone));
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_QUERY_FALLBACK))]
    private void ReceiveQueryFallback(SERVICE_101_PROTOCOL.MSG_QUERY_FALLBACK message) {
        if (_fallbackEntries.TryGetValue(message.RemoteIp, out var entry)) {
            Sender.Tell(new SERVICE_101_PROTOCOL.MSG_QUERY_FALLBACK_RSP {
                Found = true,
                UserId = entry.UserId,
                CharId = entry.CharId,
                FallbackZone = entry.FallbackZone,
                FallbackZoneId = entry.FallbackZoneId,
                FallbackLocation = entry.FallbackLocation,
                GameServerIp = entry.GameServerIp,
                GameServerPort = entry.GameServerPort
            });
        }
        else {
            Sender.Tell(new SERVICE_101_PROTOCOL.MSG_QUERY_FALLBACK_RSP { Found = false });
        }
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_REMOVE_FALLBACK))]
    private void ReceiveRemoveFallback(SERVICE_101_PROTOCOL.MSG_REMOVE_FALLBACK message) {
        _fallbackEntries.TryRemove(message.RemoteIp, out _);
    }

    protected override ushort GetNewUniqueId() {
        ushort newId = 0;
        var isUniqueId = false;
        var random = new Random();

        while (!isUniqueId) {
            newId = (ushort) random.Next(ushort.MaxValue);

            if (!ActiveSessions.Any(s => s.SessionID == newId)
                && !_playerQueue.Any(s => s.SessionID == newId)) {
                isUniqueId = true;
            }
        }

        return newId;
    }

    private void LoadResources() {
        // Load SpiralDB — the in-memory world database from JSON files.
        SpiralDB.Load();
    }

    private void ActiveSessionsChangedEvent(object obj, NotifyCollectionChangedEventArgs args) {
        // Anytime a player has left, we'll check to see if a queue is active. If so, we'll grab the next player
        // and finally allocate their slot.
        if (args.OldItems == null || _playerQueue.Count <= 0) {
            return;
        }

        // Add the first in line for each new slot available.
        for (int i = 0; i < args.OldItems.Count; i++) {
            if (_playerQueue.Count <= 0) {
                return;
            }

            var newPlayer = _playerQueue.Dequeue();
            ActiveSessions.Add(newPlayer);
            Logger.Information("{Name} New connection {RemoteEndPoint}", Logger.Args(Name, newPlayer.RemoteIp));

            // Inform the SessionActor that it's finally outside of queue.
            newPlayer.Dequeue(); ;
        }

        // Inform each enqueued player of their new position.
        for (int i = 0; i < _playerQueue.Count; i++) {
            var msg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED() {
                PrepPhase = 1,
                Slot = i
            };

            _playerQueue[i].ActorRef.Tell(msg);
        }
    }

    private ByteString CreateKey(ulong accountId) {
        var key = SessionKey.GenerateHash(_sessionKeyHashInput, accountId);

        // Add this key to the local server. We're going to map the key to an account, that way when a game
        // client finds its corresponding key, it will get it's account as well.
        var timeSpan = TimeSpan.FromSeconds(_sessionKeyValidityTime);
        _sessionKeys.Store(key, accountId, timeSpan);

        return key;
    }

    private sealed class FallbackEntry {

        public ulong UserId;
        public ulong CharId;
        public string FallbackZone;
        public uint FallbackZoneId;
        public string FallbackLocation;
        public string GameServerIp;
        public ushort GameServerPort;
        public DateTime RegisteredAt;

    }

}
