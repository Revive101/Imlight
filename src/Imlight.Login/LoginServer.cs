using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common;
using Akka.Actor;
using Imlight.Net;
using Imlight.Net.Messages;
using WizUnraveler.Cache;
using WizUnraveler.DML;

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

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERS))]
        private void ReceiveQueryGameServer(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERS message)
        {
            _gamePoolServer.Forward(message);
        }

        [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED))]
        private void ReceivePlayerEnqueued(SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED message)
        {
            // The login server does not have a queue. For now. >:(
            ActiveSessions.Add(message.SessionActor);
            
            // Inform the client they've been added to the server.
            var rsp = new LOGIN_7_PROTOCOL.MSG_USER_ADMIT_IND()
            {
                PositionInQueue = 0,
                Status = 1
            };

            Sender.Tell(rsp);
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
