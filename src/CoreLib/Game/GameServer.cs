/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Specialized;
using System.Linq;
using Akka.Actor;
using Imlight.Common.Configuration;
using Imlight.Common.Structures;
using Imlight.Common.Cryptography;
using Imlight.Common.IO;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.Common.Caches;
using Imlight.Common;

namespace Imlight.CoreLib.Game;

public class GameServer : Server {
    private readonly string _sessionKeyHashInput = ConfigurationManager.Settings.SessionKeyHashInput;
    private readonly ushort _sessionKeyValidityTime = ConfigurationManager.Settings.SessionKeyValidityTime;
    private readonly ushort _playerLimit = ConfigurationManager.Settings.GameServerPlayerLimit;

    private readonly IActorRef _gameWorldRef;
    private readonly Cache<ByteString, Account> _sessionKeys;
    private readonly ListQueue<SessionActor> _playerQueue;

    public GameServer(string serverName, ushort serverPort)
        : base(serverName, serverPort, GameServiceFactory.Props()) {
        this._playerQueue = new ListQueue<SessionActor>();
        this._sessionKeys = new Cache<ByteString, Account>();
        this.ActiveSessions.CollectionChanged += ActiveSessionsChangedEvent;

        // Create actor children.
        var gameWorldActorName = $"{Name}.GameWorld";
        _gameWorldRef = Context.ActorOf(GameWorld.Props(this), gameWorldActorName);
        Logger.Verbose("New actor created under {Path}: {Name}",
            Logger.Args(Context.Self.Path, gameWorldActorName));

        // Log
        Logger.Information("Game server created with name {Name} under port {Port}.",
            Logger.Args(serverName, serverPort));
    }

    public static Props Props(string serverName, ushort serverPort) {
        return Akka.Actor.Props.Create(() => new GameServer(serverName, serverPort));
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEKEY))]
    private void ReceiveCreateKey(SERVER_100_PROTOCOL.MSG_CREATEKEY message) {
        var key = CreateKey(message.Account);

        var rsp = new SERVER_100_PROTOCOL.MSG_CREATEKEYRSP() { Key = key };
        Sender.Tell(rsp);
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
            //_sessionKeys.Remove(cachedKey.Key);

            // Inform the client that the session key is valid. We'll also send the account associated with it.
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP() {
                ErrorCode = 0,
                Account = cachedKey.Value
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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
    private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message) {
        _gameWorldRef.Forward(message);
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

    private ByteString CreateKey(Account account) {
        var key = SessionKey.GenerateHash(_sessionKeyHashInput, account.AccountId);

        // Add this key to the local server. We're going to map the key to an account, that way when a game
        // client finds its corresponding key, it will get it's account as well.
        var timeSpan = TimeSpan.FromSeconds(_sessionKeyValidityTime);
        _sessionKeys.Store(key, account, timeSpan);

        return key;
    }
}
