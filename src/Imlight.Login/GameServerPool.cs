using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common;
using Akka.Actor;
using Imlight.Game;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler;

namespace Imlight.Login
{
    internal class GameServerPool : ReceiveProtocolDispatcher
    {
        private const byte ALLOWED_GAME_SERVER_COUNT = 3;
        
        private Dictionary<ushort, IActorRef> _gameServers;

        public GameServerPool()
        {
            this._gameServers = new Dictionary<ushort, IActorRef>();
        }
        
        public static Props Props()
        {
            return Akka.Actor.Props.Create(() => new GameServerPool());
        }
        
        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER))]
        private void ReceiveCreateGameServer(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER message)
        {
            if (_gameServers.Count >= ALLOWED_GAME_SERVER_COUNT)
            {
                Log.Logger.Error($"GameServerPoolActor attempted to create a new game server, but the " +
                                 $"internal limit has already been reached. Server has not been created.");
                return;
            }
            
            //var port = GetUnusedPort();
            // The props method of the GameServer will return both the instance of the object as well as the
            // props for it. We're keeping the instance allocated here, that way we dont need to send so many
            // messages for each player connection.
            var gameProps = GameServer.Props(Context.Self, message.Name, message.Port);
            var gameServerRef = Context.ActorOf(gameProps, $"{message.Name}_{message.Port}");

            _gameServers.Add(message.Port, gameServerRef);
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVER))]
        private void ReceiveQueryGameServer(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVER message)
        {
            // A login SessionActor has queried the pool looking for the best possible game server to connect to.
            // Ask each game server for it's details.
            var gameServers = new List<SERVER_100_PROTOCOL.MSG_GAMESERVER>();
            foreach (var gameServer in _gameServers.Values)
            {
                var msg = new SERVER_100_PROTOCOL.MSG_GAMESERVERDETAILS();
                var rsp = gameServer.Ask<SERVER_100_PROTOCOL.MSG_GAMESERVER>(msg)
                    .Result;

                gameServers.Add(rsp);
            }

            // Sort the servers by popularity, removing any servers that are full.
            gameServers
                .Where(s => s.PlayerCount < Server.MAX_PLAYER_COUNT)
                .ToList()
                .Sort((s1, s2) => s2.PlayerCount.CompareTo(s1.PlayerCount));
            
            // If each server is full, place the login actor into the queue of a random one.
            if (gameServers.Count <= 0)
            {
                // @todo: place in queue
            }
            
            // The best server will simply be the one with the most players, that isn't full.
            var chosenServer = gameServers[0];
            
            // Now that we have our best game server, send another message to the game server to create a session key.
            // The game server will save that key for a limited amount of time. When the client attaches to the
            // game server again, the game server will check their key to make sure they didn't skip the login server.
            var keyMsg = new SERVER_100_PROTOCOL.MSG_CREATEKEY()
            {
                SessionID = message.SessionActor.SessionID
            };
            var keyRsp = chosenServer.ActorRef.Ask<SERVER_100_PROTOCOL.MSG_CREATEKEYRSP>(keyMsg)
                .Result;

            var responseMsg = new SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERRSP()
            {
                IP = chosenServer.IP,
                Port = chosenServer.Port,
                Key = keyRsp.Key
            };
            
            Sender.Tell(responseMsg);
        }
        
        private ushort GetUnusedPort()
        {
            var rand = new Random();
            while (true)
            {
                var maxClamp = GameServer.DEFAULT_GAME_SERVER_PORT + ALLOWED_GAME_SERVER_COUNT;
                var temp = rand.Next(GameServer.DEFAULT_GAME_SERVER_PORT, maxClamp);
                if (_gameServers.Keys.All(x => x != temp))
                {
                    return (ushort)temp;
                }
            }
        }
    }
}
