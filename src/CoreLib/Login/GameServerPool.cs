/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.Common.Configuration;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Game;
using Imlight.Common;

namespace Imlight.CoreLib.Login;

internal class GameServerPool : ReceiveProtocolDispatcher {
    private readonly byte _maxGameServersAllowed = ConfigurationManager.Settings.MaxGameServersAllowed;
    private readonly ushort _gameServerPlayerCount = ConfigurationManager.Settings.GameServerPlayerLimit;

    private readonly Dictionary<ushort, IActorRef> _gameServers;

    public GameServerPool() {
        this._gameServers = new Dictionary<ushort, IActorRef>();

        Logger.Information("GameServerPool created.");
    }

    public static Props Props() {
        return Akka.Actor.Props.Create(() => new GameServerPool());
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER))]
    private void ReceiveCreateGameServer(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER message) {
        if (_gameServers.Count >= _maxGameServersAllowed) {
            Logger.Error("{Type} attempted to create a new game server, but the " +
                      $"internal limit has already been reached. Server has not been created.",
                Logger.Args(GetType()));
            return;
        }
        if (_gameServers.Keys.Any(x => x == message.Port)) {
            Logger.Error("{Type} attempted to create a new game server, but the port" +
                      " {Port} was already in use", Logger.Args(GetType(), message.Port));
            return;
        }

        var gameProps = GameServer.Props(message.Name, message.Port);
        var gameServerRef = Context.ActorOf(gameProps, $"{message.Name}.{message.Port}");

        _gameServers.Add(message.Port, gameServerRef);

        Logger.Verbose("New actor created under {Path}: {Name}.{Port}",
            Logger.Args(Context.Self.Path, message.Name, message.Port));
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_GETBESTSERVER))]
    private void ReceiveQueryGameServer(SERVER_100_PROTOCOL.MSG_GETBESTSERVER message) {
        // Create a list of game servers and query each server for its details
        var gameServers = _gameServers.Values
            .Select(gameServer => {
                var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER() { IsLocal = message.IsLocal };
                var rsp = gameServer.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg).Result;
                return rsp;
            })
            .ToList();

        // Sort the servers by player count in descending order
        gameServers.Sort((s1, s2) => s2.PlayerCount.CompareTo(s1.PlayerCount));

        // Find the first non-full server or choose a random one if all servers are full
        var chosenServer = gameServers
                               .FirstOrDefault(server => server.PlayerCount < _gameServerPlayerCount)
                           ?? gameServers[new Random().Next(0, gameServers.Count)];

        // Send the chosen server details back to the session actor
        Sender.Tell(chosenServer);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_FINDPLAYER))]
    private void ReceiveFindPlayer(SERVER_100_PROTOCOL.MSG_FINDPLAYER message) {
        var gameServers = _gameServers.Values
            .Select(gameServer => {
                var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();
                var rsp = gameServer.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg).Result;
                return rsp;
            })
            .ToList();

        foreach (var server in gameServers) {
            var connectedPlayers = server.ConnectedIps;
            if (server.ConnectedIps.Contains(message.Ip)) {
                var foundMsg = new SERVER_100_PROTOCOL.MSG_PLAYERFOUND() {
                    Found = true,
                };
                Sender.Tell(foundMsg);
                return;
            }
        }

        // Inform the sender that this player was not found on any game servers.
        var failureMsg = new SERVER_100_PROTOCOL.MSG_PLAYERFOUND() {
            Found = false,
        };
        Sender.Tell(failureMsg);
    }
}
