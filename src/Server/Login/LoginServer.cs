/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Configuration;
using Imlight.Common.Serializable.Caches;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;

namespace Imlight.Server.Login;

public class LoginServer : Shared.Networking.Server
{
    private readonly IActorRef _gamePoolServer;
    private const string GameServerPoolName = "GameServerPool";

    public LoginServer(string serverName, ushort serverPort)
        : base(serverName, serverPort, LoginServiceFactory.Props())
    {
        this._gamePoolServer = CreateGameServerPool();
        Log.Information("Login server created with name {Name} under port {Port}.", 
            Log.Args(serverName, serverPort));
    }
        
    public static Props Props(string serverName, ushort serverPort)
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

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER))]
    private void ReceiveCreateGameServer(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER message)
    {
        var defaultGameServerName = ConfigurationManager.Settings.GameServerName;
        var defaultGameServerPort = ConfigurationManager.Settings.GameServerPort;
            
        // If the name or port is not set, use the default values.
        if (message.Name is "" or null)
            message.Name = defaultGameServerName;
        if (message.Port == 0)
            message.Port = defaultGameServerPort;
            
        _gamePoolServer.Tell(message);
    }

    private IActorRef CreateGameServerPool()
    {
        var poolProps = GameServerPool.Props();

        Log.Verbose("New actor created under {Path}: {Name}.{PoolName}", 
            Log.Args(Context.Self.Path, Name, GameServerPoolName));
            
        return Context.ActorOf(poolProps, $"{Name}.{GameServerPoolName}");
    }
}