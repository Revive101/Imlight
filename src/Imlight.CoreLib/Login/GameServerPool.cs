/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * GAME SERVER POOL
 * ========================================================================
 * 
 * PURPOSE:
 * Manages a pool of game servers, handles server creation, queries for best server 
 * selection, and player location tracking across the network.
 * 
 * USAGE EXAMPLE:
 * var gameServerPool = system.ActorOf(GameServerPool.Props(), "gameServerPool");
 * gameServerPool.Tell(new SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER { Name = "Server1", Port = 8080 });
 * 
 * NOTE:
 * Requires Akka.NET for actor-based concurrency. Uses Ask pattern with timeouts
 * for server queries which may impact performance under heavy load.
 *
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Game;
using Imlight.Common;

namespace Imlight.CoreLib.Login;

internal class GameServerPool : ReceiveProtocolDispatcher {

    private readonly byte _maxGameServersAllowed 
        = ConfigurationManager.Settings["Game Server.MaxGameServersAllowed"].AsByte();
    private readonly ushort _gameServerPlayerCount 
        = ConfigurationManager.Settings["Game Server.GameServerPlayerLimit"].AsUShort();
    private readonly ushort _gameServerQueryTimeout = 10;

    private readonly Dictionary<ushort, IActorRef> _gameServers;
    private readonly Dictionary<string, ushort> _realmToPort = [];

    public GameServerPool() {
        this._gameServers = [];

        Logger.Information("GameServerPool created.");
    }

    public static Props Props() 
        => Akka.Actor.Props.Create(() => new GameServerPool());

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

        var gameProps = GameServer.Props(message.Name, message.Port, message.RealmName);
        var gameServerRef = Context.ActorOf(gameProps, $"{message.Name}.{message.Port}");

        _gameServers.Add(message.Port, gameServerRef);

        if (!string.IsNullOrEmpty(message.RealmName)) {
            _realmToPort[message.RealmName] = message.Port;
        }

        Logger.Debug("Game server pool registered new game server {Name} (realm: {Realm}) on port {Port}.",
            Logger.Args(message.Name, message.RealmName, message.Port));
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_GETBESTSERVER))]
    private void ReceiveQueryGameServer(SERVER_100_PROTOCOL.MSG_GETBESTSERVER message) {
        // Iterate through the game servers we have registered and query them to check if they're still active.
        var gameServerInfos = new List<SERVER_100_PROTOCOL.MSG_SERVERINFO>();
        foreach (var gameServer in _gameServers.Values) {
            try {
                var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();
                var timeout = TimeSpan.FromSeconds(_gameServerQueryTimeout);
                var rsp = gameServer.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg, timeout).Result;
                gameServerInfos.Add(rsp);
            }
            catch {
                // The server did not respond in time, so we'll just ignore it.
                Logger.Error("Failed to query game server {Name}.", Logger.Args(gameServer.Path.Name));

                continue;
            }
        }

        if (gameServerInfos.Count <= 0) {
            throw new Exception("No game servers were available to query.");
        }

        // Sort the servers by player count in descending order
        gameServerInfos.Sort((s1, s2) => s2.PlayerCount.CompareTo(s1.PlayerCount));

        // Find the first non-full server or choose a random one if all servers are full
        var chosenServer = gameServerInfos
                               .FirstOrDefault(server => server.PlayerCount < _gameServerPlayerCount)
                           ?? gameServerInfos[new Random().Next(0, gameServerInfos.Count)];

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

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEY))]
    private void ReceiveCreatePlayerKey(SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEY message) {
        if (!_realmToPort.TryGetValue(message.TargetRealmName, out var port)
            || !_gameServers.TryGetValue(port, out var serverRef)) {
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_CREATEPLAYERKEYRSP { Success = false });

            return;
        }

        // Forward the key creation request to the target game server.
        // The game server handles MSG_CREATEPLAYERKEY and returns MSG_CREATEPLAYERKEYRSP.
        serverRef.Forward(message);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYREALMSERVER))]
    private void ReceiveQueryRealmServer(SERVER_100_PROTOCOL.MSG_QUERYREALMSERVER message) {
        if (!_realmToPort.TryGetValue(message.RealmName, out var port)
            || !_gameServers.TryGetValue(port, out var serverRef)) {
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_SERVERINFO { RealmName = null });

            return;
        }

        try {
            var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();
            var timeout = TimeSpan.FromSeconds(_gameServerQueryTimeout);
            var rsp = serverRef.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg, timeout).Result;
            Sender.Tell(rsp);
        }
        catch {
            Sender.Tell(new SERVER_100_PROTOCOL.MSG_SERVERINFO { RealmName = null });
        }
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_REALMLIST))]
    private void ReceiveRealmList(SERVER_100_PROTOCOL.MSG_REALMLIST message) {
        var realmNames = new List<string>();
        var playerCounts = new List<ushort>();
        var playerLimits = new List<ushort>();

        foreach (var (realmName, port) in _realmToPort) {
            if (!_gameServers.TryGetValue(port, out var serverRef)) {
                continue;
            }

            try {
                var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();
                var timeout = TimeSpan.FromSeconds(_gameServerQueryTimeout);
                var rsp = serverRef.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg, timeout).Result;

                realmNames.Add(realmName);
                playerCounts.Add(rsp.PlayerCount);
                playerLimits.Add(_gameServerPlayerCount);
            }
            catch {
                // Server unreachable — skip it.
            }
        }

        Sender.Tell(new SERVER_100_PROTOCOL.MSG_REALMLIST {
            RealmNames = [.. realmNames],
            PlayerCounts = [.. playerCounts],
            PlayerLimits = [.. playerLimits]
        });
    }

}
