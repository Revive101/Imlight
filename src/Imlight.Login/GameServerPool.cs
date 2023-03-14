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
            // Create a list of game servers and query each server for its details
            var gameServers = _gameServers.Values
                .Select(gameServer =>
                {
                    var msg = new SERVER_100_PROTOCOL.MSG_QUERYGAMESERVER();
                    var rsp = gameServer.Ask<SERVER_100_PROTOCOL.MSG_GAMESERVER>(msg).Result;
                    return rsp;
                })
                .ToList();

            // Sort the servers by player count in descending order
            gameServers.Sort((s1, s2) => s2.PlayerCount.CompareTo(s1.PlayerCount));

            // Find the first non-full server or choose a random one if all servers are full
            var chosenServer = gameServers.FirstOrDefault(server => server.PlayerCount < GameServer.MAX_PLAYER_COUNT)
                               ?? gameServers[new Random().Next(0, gameServers.Count)];

            // Send the chosen server details back to the session actor
            Sender.Tell(chosenServer);
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
