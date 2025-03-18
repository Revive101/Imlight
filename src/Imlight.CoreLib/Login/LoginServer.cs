/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Login;

public class LoginServer : Server {

    private readonly IActorRef _gamePoolServer;
    private const string GameServerPoolName = "GameServerPool";

    public LoginServer(string serverName, ushort serverPort)
        : base(serverName, serverPort, LoginServiceFactory.Props()) {
        this._gamePoolServer = CreateGameServerPool();

        Logger.Information("Login server created with name {Name} under port {Port}.",
            Logger.Args(serverName, serverPort));
    }

    public static Props Props(string serverName, ushort serverPort) 
        => Akka.Actor.Props.Create(() => new LoginServer(serverName, serverPort));

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_GETBESTSERVER))]
    private void ReceiveQueryGameServer(SERVER_100_PROTOCOL.MSG_GETBESTSERVER message) {
        _gamePoolServer.Forward(message);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED))]
    private void ReceivePlayerEnqueued(SERVER_100_PROTOCOL.MSG_PLAYERENQUEUED message) {
        var rsp = new SERVER_100_PROTOCOL.MSG_PLAYERENQUEUEDRSP();

#if !DEBUG
        // If this IP is already in the login server or any game server, deny entry.
        //var findPlayerMsg = new SERVER_100_PROTOCOL.MSG_FINDPLAYER() { Ip = message.SessionActor.RemoteIp };
        //var foundPlayerRsp = _gamePoolServer.Ask<SERVER_100_PROTOCOL.MSG_PLAYERFOUND>(findPlayerMsg).Result.Found;
        //if (ActiveSessions.Any(x => x.RemoteIp == message.SessionActor.RemoteIp) || foundPlayerRsp) {
        //    rsp.Failed = true;
        //    Sender.Tell(rsp);

        //    return;
        //}
#endif

        // The login server does not have a queue. For now. >:(
        ActiveSessions.Add(message.SessionActor);

        // Inform the client they've been added to the server.
        rsp.PositionInQueue = 0;
        rsp.Status = 1;
        Sender.Tell(rsp);

        // The client will close the socket after being placed into the game server. This message is sent
        // to all the SessionActor services to inform them to halt their operations.
        message.SessionActor.ActorRef.Tell(new SERVICE_101_PROTOCOL.MSG_OPCODE_HALT());

        Logger.Information("{Name} new connection {RemoteEndPoint}", Logger.Args(Name, message.SessionActor.RemoteIp));
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER))]
    private void ReceiveCreateGameServer(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER message) {
        var defaultGameServerName = ConfigurationManager.Settings["Game Server.GameServerName"].AsString();
        var defaultGameServerPort = ConfigurationManager.Settings["Game Server.GameServerPort"].AsUShort();

        // If the name or port is not set, use the default values.
        if (message.Name is "" or null) {
            message.Name = defaultGameServerName;
        }

        if (message.Port == 0) {
            message.Port = defaultGameServerPort;
        }

        _gamePoolServer.Tell(message);
    }

    private IActorRef CreateGameServerPool() {
        var poolProps = GameServerPool.Props();

        Logger.Verbose("New actor created under {Path}: {Name}.{PoolName}",
            Logger.Args(Context.Self.Path, Name, GameServerPoolName));

        return Context.ActorOf(poolProps, $"{Name}.{GameServerPoolName}");
    }

}
