using System;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Actor.Dsl;
using Imlight.Common;
using Imlight.Common.Crypto;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler;

namespace Imlight.Game
{
    public class GameServer : Server
    {
        public const string DEFAULT_GAME_SERVER_NAME = "Imlight.Game";
        public const ushort DEFAULT_GAME_SERVER_PORT = 12333;
        public const string SESSION_KEY_HASH_INPUT = "MAGIC_HATTER";
        public const ushort SESSION_KEY_VALIDITY_TIME = 120;

        private IActorRef _serverPoolRef;
        private TimedList<ByteString> _sessionKeys;

        public GameServer(IActorRef serverPoolRef,
                          string serverName = DEFAULT_GAME_SERVER_NAME,
                          ushort serverPort = DEFAULT_GAME_SERVER_PORT)
                          : base(serverName, serverPort, GameServiceFactory.Props())
        {
            this._serverPoolRef = serverPoolRef;
            
            // Session keys are valid for x seconds.
            this._sessionKeys = new TimedList<ByteString>(SESSION_KEY_VALIDITY_TIME);
            
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

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_GAMESERVERDETAILS))]
        private void ReceiveGameServerDetails(SERVER_100_PROTOCOL.MSG_GAMESERVERDETAILS message)
        {
            var msg = new SERVER_100_PROTOCOL.MSG_GAMESERVER()
            {
                IP = IP,
                Port = Port,
                PlayerCount = (ushort)ConnectedPlayers.Count,
                ActorRef = Context.Self,
            };
            
            Sender.Tell(msg);
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEKEY))]
        private void ReceiveCreateKey(SERVER_100_PROTOCOL.MSG_CREATEKEY message)
        {
            var key = SessionKey.GenerateHash(SESSION_KEY_HASH_INPUT, message.SessionID);

            // Add this key to the local server.
            _sessionKeys.Add(key);
            
            var rsp = new SERVER_100_PROTOCOL.MSG_CREATEKEYRSP()
            {
                Key = key
            };
            
            Sender.Tell(rsp);
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY))]
        private void ReceiveValidateSessionKey(SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEY message)
        {
            var validation = SessionKey.ValidateHash(
                SESSION_KEY_HASH_INPUT, 
                message.SessionID, 
                message.Key);

            var rsp = new SERVER_100_PROTOCOL.MSG_VALIDATESESSIONKEYRSP()
            {
                ErrorCode = validation ? 1 : 0,
            };
            Sender.Tell(rsp);
        }
    }
}