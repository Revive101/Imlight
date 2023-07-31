/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Specialized;
using System.Linq;
using Akka.Actor;
using Akka.Actor.Dsl;
using WizUnraveler;
using WizUnraveler.Cache;
using Imlight.Common.Structures;
using Imlight.Common.Utilities;
using Imlight.Common.Cryptography;
using Imlight.Server.Database;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using WizUnraveler.IO;

namespace Imlight.Server.Game
{
    public class GameServer : Shared.Networking.Server
    {
        private const string DEFAULT_GAME_SERVER_NAME = "Imlight.Game";
        private const ushort DEFAULT_GAME_SERVER_PORT = 12333;
        private const string SESSION_KEY_HASH_INPUT = "MAGIC_HATTER";
        private const ushort SESSION_KEY_VALIDITY_TIME = 28800; // In seconds; 8 hours
        
        private IActorRef _gameWorldRef;
        private Cache<ByteString, Account> _sessionKeys;
        private readonly ListQueue<SessionActor> _playerQueue;

        public GameServer(string serverName = DEFAULT_GAME_SERVER_NAME,
                          ushort serverPort = DEFAULT_GAME_SERVER_PORT)
                          : base(serverName, serverPort, GameServiceFactory.Props())
        {
            this._playerQueue = new ListQueue<SessionActor>();
            this._sessionKeys = new Cache<ByteString, Account>();
            
            this.ActiveSessions.CollectionChanged += ActiveSessionsChangedEvent;
            
            // Create actor children.
            var gameWorldActorName = $"{Name}.GameWorld";
            _gameWorldRef = Context.ActorOf(GameWorld.Props(this), gameWorldActorName);
            Log.Verbose("New actor created under {Path}: {Name}",
                Log.Args(Context.Self.Path, gameWorldActorName));

            // Log
            Log.Information("Game server created with name {Name} under port {Port}.",
                Log.Args(serverName, serverPort));
        }
        
        public static Props Props(string serverName = DEFAULT_GAME_SERVER_NAME,
                                  ushort serverPort = DEFAULT_GAME_SERVER_PORT)
        {
            return Akka.Actor.Props.Create(() => new GameServer(serverName, serverPort));
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEKEY))]
        private void ReceiveCreateKey(SERVER_100_PROTOCOL.MSG_CREATEKEY message)
        {
            var key = CreateKey(message.Account);
            
            var rsp = new SERVER_100_PROTOCOL.MSG_CREATEKEYRSP() { Key = key };
            Sender.Tell(rsp);
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY))]
        private void ReceiveValidateSessionKey(SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY message)
        {
            // A user has requested to join this server. We're going to check if the session key is valid.
            // If it is, we'll return the account associated with it. If not, we'll return an error code.
            var keyTest = SessionKey.GenerateHash(SESSION_KEY_HASH_INPUT, message.UserID);

            foreach (var cachedKey in _sessionKeys)
            {
                if (keyTest != cachedKey.Key) continue;
                
                ActiveSessions.Add(message.SessionActor);
                //_sessionKeys.Remove(cachedKey.Key);
                
                // Inform the client that the session key is valid. We'll also send the account associated with it.
                Sender.Tell(new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP()
                {
                    ErrorCode = 0,
                    Account = cachedKey.Value
                });

                return;
            }
            
            // The session key was not found in the cache. Return an error code.
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP() { ErrorCode = 1 });
        }
        
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED))]
        private void ReceivePlayerEnqueued(SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED message)
        {
            // A player has requested to join this server.
            var rsp = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED()
            {
                PrepPhase = 0,
                Slot = 0
            };
            
            // If this is a VIP, we'll let them in immediately.
            if (message.VIPEntry)
            {
                Sender.Tell(rsp);
                return;
            }

            // If the server is full, add them to the queue and inform the client.
            if (ActiveSessions.Count >= PLAYER_LIMIT)
            {
                _playerQueue.Enqueue(message.SessionActor);
                var queuePos = _playerQueue.Count;
                
                message.SessionActor.PlaceInQueue((ushort)queuePos);
                
                rsp.PrepPhase = 1;
                rsp.Slot = queuePos;
            }
            
            // Only a session that exists on the login server will even bother trying to enqueue itself.
            // Meaning that we don't actually want to add it to the active sessions here.
            Sender.Tell(rsp);
        }
        
        [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONETRANSFER))]
        private void ReceiveZoneTransferRequest(ZONE_102_PROTOCOL.MSG_ZONETRANSFER message)
        {
            _gameWorldRef.Forward(message);
        }
        
        protected override ushort GetNewUniqueId()
        {
            ushort newId = 0;
            var isUniqueId = false;
            var random = new Random();

            while (!isUniqueId)
            {
                newId = (ushort)random.Next(ushort.MaxValue);

                if (!ActiveSessions.Any(s => s.SessionID == newId) 
                    && !_playerQueue.Any(s => s.SessionID == newId))
                {
                    isUniqueId = true;
                }
            }

            return newId;
        }

        private void ActiveSessionsChangedEvent(object obj, NotifyCollectionChangedEventArgs args)
        {
            // Anytime a player has left, we'll check to see if a queue is active. If so, we'll grab the next player
            // and finally allocate their slot.
            if (args.OldItems == null || _playerQueue.Count <= 0)
                return;

            // Add the first in line for each new slot available.
            for (int i = 0; i < args.OldItems.Count; i++)
            {
                if (_playerQueue.Count <= 0)
                    return;

                var newPlayer = _playerQueue.Dequeue();
                ActiveSessions.Add(newPlayer);
                
                // Inform the SessionActor that it's finally outside of queue.
                newPlayer.Dequeue();;
            }
            
            // Inform each enqueued player of their new position.
            for (int i = 0; i < _playerQueue.Count; i++)
            {
                var msg = new LOGIN_7_PROTOCOL.MSG_CHARACTERSELECTED()
                {
                    PrepPhase = 1,
                    Slot = i
                };
                
                _playerQueue[i].ActorRef.Tell(msg);
            }
        }

        private ByteString CreateKey(Account account)
        {
            var key = SessionKey.GenerateHash(SESSION_KEY_HASH_INPUT, account.ID);
            
            // Add this key to the local server. We're going to map the key to an account, that way when a game
            // client finds its corresponding key, it will get it's account as well.
            var timeSpan = TimeSpan.FromSeconds(SESSION_KEY_VALIDITY_TIME);
            _sessionKeys.Store(key, account, timeSpan);

            return key;
        }
    }
}