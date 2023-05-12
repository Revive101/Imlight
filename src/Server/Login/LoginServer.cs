using Akka.Actor;
using WizUnraveler.Cache;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Login
{
    public class LoginServer : Shared.Networking.Server
    {
        public const string DEFAULT_LOGIN_SERVER_NAME = "Imlight.Login";
        private const ushort DEFAULT_LOGIN_SERVER_PORT = 12000;
        private const string DEFAULT_LOCAL_GAME_SERVER_NAME = "Imlight.Game";
        private const ushort DEFAULT_LOCAL_GAME_SERVER_PORT = 12333;
        private const string GAME_SERVER_POOL_NAME = "GameServerPool";

        private IActorRef _gamePoolServer;

        public LoginServer(string serverName = DEFAULT_LOGIN_SERVER_NAME,
                           ushort serverPort = DEFAULT_LOGIN_SERVER_PORT)
                           : base(serverName, serverPort, LoginServiceFactory.Props())
        {
            this._gamePoolServer = CreateGameServerPool();

            CreateLocalServer();
            Log.Logger.Debug($"New actor created under {Context.Self.Path}:" +
                             $" {DEFAULT_LOGIN_SERVER_NAME}.{GAME_SERVER_POOL_NAME}");
            
            Log.Logger.Information($"Login server created with " +
                                   $"name {serverName} " +
                                   $"under port {serverPort}.");
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
            
            // The client will close the socket after being placed into the game server. This message is sent
            // to all the SessionActor services to inform them to halt their operations.
            message.SessionActor.ActorRef.Tell(new SERVICE_101_PROTOCOL.MSG_OPCODE_HALT());
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

            return Context.ActorOf(poolProps, $"{Name}.{GAME_SERVER_POOL_NAME}");
        }
    }
}
