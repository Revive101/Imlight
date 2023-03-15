using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Akka.Actor;
using Akka.Actor.Dsl;
using DotNetty.Codecs;
using Imlight.Common;
using Imlight.Common.Crypto;
using Imlight.Data;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler;
using WizUnraveler.Cache;

namespace Imlight.Game
{
    public class GameServer : Server
    {
        public const string DEFAULT_GAME_SERVER_NAME = "Imlight.Game";
        public const ushort DEFAULT_GAME_SERVER_PORT = 12333;
        public const string SESSION_KEY_HASH_INPUT = "MAGIC_HATTER";
        public const ushort SESSION_KEY_VALIDITY_TIME = 120;

        private IActorRef _serverPoolRef;
        private Cache<ByteString, Account> _sessionKeys;

        public GameServer(IActorRef serverPoolRef,
                          string serverName = DEFAULT_GAME_SERVER_NAME,
                          ushort serverPort = DEFAULT_GAME_SERVER_PORT)
                          : base(serverName, serverPort, GameServiceFactory.Props())
        {
            this._serverPoolRef = serverPoolRef;
            
            // Session keys are valid for x seconds.
            this._sessionKeys = new Cache<ByteString, Account>();
            
            Log.Logger.Information($"Game server created with " +
                                   $"name {serverName} " +
                                   $"under port {serverPort}.");
        }
        
        public static Props Props(IActorRef serverPoolRef,
                                  string serverName = DEFAULT_GAME_SERVER_NAME,
                                  ushort serverPort = DEFAULT_GAME_SERVER_PORT)
        {
            return Akka.Actor.Props.Create(() => new GameServer(serverPoolRef, serverName, serverPort));
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEKEY))]
        private void ReceiveCreateKey(SERVER_100_PROTOCOL.MSG_CREATEKEY message)
        {
            var key = SessionKey.GenerateHash(SESSION_KEY_HASH_INPUT, message.Account.ID);

            // Add this key to the local server. We're going to map the key to an account, that way when a game
            // client finds its corresponding key, it will get it's account as well.
            var timeSpan = TimeSpan.FromSeconds(SESSION_KEY_VALIDITY_TIME);
            _sessionKeys.Store(key, message.Account, timeSpan);
            
            var rsp = new SERVER_100_PROTOCOL.MSG_CREATEKEYRSP()
            {
                Key = key,
            };
            
            Sender.Tell(rsp);
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY))]
        private void ReceiveValidateSessionKey(SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY message)
        {
            // A transitioning game client has given us a session key. We're going to create a key from the 
            // session details it gave us and see if any of our keys match.
            var keyTest = SessionKey.GenerateHash(SESSION_KEY_HASH_INPUT, message.UserID);

            foreach (var cachedKey in _sessionKeys)
            {
                if (keyTest != cachedKey.Key) continue;
                
                Sender.Tell(new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP()
                {
                    ErrorCode = 0,
                    Account = cachedKey.Value
                });

                return;
            }
            
            // The session key was not found in the cache. Return an error code.
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP()
            {
                ErrorCode = 1
            });
        }
    }
}