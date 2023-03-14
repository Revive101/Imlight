using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common;
using Akka.Actor;
using Imlight.Net;
using Imlight.Net.Messages;

namespace Imlight.Login
{
    public class LoginServer : Server
    {
        private const string DEFAULT_LOGIN_SERVER_NAME = "Imlight.Login";
        private const ushort DEFAULT_LOGIN_SERVER_PORT = 12000;
        private const string DEFAULT_LOCAL_GAME_SERVER_NAME = "Imlight.Game";
        private const ushort DEFAULT_LOCAL_GAME_SERVER_PORT = 12333;
        
        private IActorRef _gamePoolServer;

        public LoginServer(string serverName = DEFAULT_LOGIN_SERVER_NAME,
                           ushort serverPort = DEFAULT_LOGIN_SERVER_PORT)
                           : base(serverName, serverPort, LoginServiceFactory.Props())
        {
            this._gamePoolServer = CreateGameServerPool();
            
            Log.Logger.Information($"Login server created with " +
                                   $"name {serverName} " +
                                   $"under port {serverPort}.");
            
            CreateLocalServer();
        }
        
        public static Props Props(string serverName = DEFAULT_LOGIN_SERVER_NAME,
                                  ushort serverPort = DEFAULT_LOGIN_SERVER_PORT)
        {
            return Akka.Actor.Props.Create(() => new LoginServer(serverName, serverPort));
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVER))]
        private void ReceiveQueryGameServer(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVER message)
        {
            _gamePoolServer.Forward(message);
        }

        private void CreateLocalServer()
        {
            var msg = new SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER()
            {
                Name = DEFAULT_LOCAL_GAME_SERVER_NAME,
                Port = DEFAULT_LOCAL_GAME_SERVER_PORT
            };
            
            _gamePoolServer.Tell(msg);
        }

        private IActorRef CreateGameServerPool()
        {
            var poolProps = GameServerPool.Props();

            return Context.ActorOf(poolProps, "GameServerPool");
        }
    }
}
