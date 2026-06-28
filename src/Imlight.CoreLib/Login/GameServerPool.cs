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
 * Last Updated: 06/27/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        // Capture the sender before any async work.
        var originalSender = Sender;

        var queryTasks = _gameServers.Values.Select(async gameServer => {
            try {
                var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();
                var timeout = TimeSpan.FromSeconds(_gameServerQueryTimeout);
                return await gameServer.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg, timeout);
            }
            catch {
                // The server did not respond in time, so we'll just ignore it.
                Logger.Error("Failed to query game server {Name}.", Logger.Args(gameServer.Path.Name));
                return null;
            }
        });

        // Aggregate results and pipe back to self for final processing.
        Task.WhenAll(queryTasks)
            .ContinueWith(t => new SERVER_100_PROTOCOL.MSG_QUERYGAMESERVER_AGGREGATE {
                OriginalSender = originalSender,
                ServerInfos = t.Result.Where(r => r != null).ToArray()
            })
            .PipeTo(Self);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVER_AGGREGATE))]
    private void ReceiveQueryGameServerAggregate(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVER_AGGREGATE result) {
        var gameServerInfos = result.ServerInfos;

        if (gameServerInfos.Length <= 0) {
            Logger.Error("No game servers were available to query.");
            return;
        }

        // Sort the servers by player count in descending order
        Array.Sort(gameServerInfos, (s1, s2) => s2.PlayerCount.CompareTo(s1.PlayerCount));

        // Find the first non-full server or choose a random one if all servers are full
        var chosenServer = gameServerInfos
                               .FirstOrDefault(server => server.PlayerCount < _gameServerPlayerCount)
                           ?? gameServerInfos[new Random().Next(0, gameServerInfos.Length)];

        // Send the chosen server details back to the original requester
        result.OriginalSender.Tell(chosenServer);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_FINDPLAYER))]
    private void ReceiveFindPlayer(SERVER_100_PROTOCOL.MSG_FINDPLAYER message) {
        var originalSender = Sender;
        var targetIp = message.Ip;

        // Query all game servers concurrently for their connected IPs.
        var queryTasks = _gameServers.Values.Select(async gameServer => {
            try {
                var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();
                return await gameServer.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg);
            }
            catch {
                return null;
            }
        });

        Task.WhenAll(queryTasks)
            .ContinueWith(t => new SERVER_100_PROTOCOL.MSG_FINDPLAYER_AGGREGATE {
                OriginalSender = originalSender,
                TargetIp = targetIp,
                ServerInfos = t.Result.Where(r => r != null).ToArray()
            })
            .PipeTo(Self);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_FINDPLAYER_AGGREGATE))]
    private void ReceiveFindPlayerAggregate(SERVER_100_PROTOCOL.MSG_FINDPLAYER_AGGREGATE result) {
        foreach (var server in result.ServerInfos) {
            if (server.ConnectedIps.Contains(result.TargetIp)) {
                result.OriginalSender.Tell(new SERVER_100_PROTOCOL.MSG_PLAYERFOUND { Found = true });
                return;
            }
        }

        // Inform the sender that this player was not found on any game servers.
        result.OriginalSender.Tell(new SERVER_100_PROTOCOL.MSG_PLAYERFOUND { Found = false });
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
        var originalSender = Sender;

        if (!_realmToPort.TryGetValue(message.RealmName, out var port)
            || !_gameServers.TryGetValue(port, out var serverRef)) {
            originalSender.Tell(new SERVER_100_PROTOCOL.MSG_SERVERINFO { RealmName = null });
            return;
        }

        var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();
        var timeout = TimeSpan.FromSeconds(_gameServerQueryTimeout);

        serverRef.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg, timeout)
            .ContinueWith(t => {
                if (t.IsFaulted || t.IsCanceled) {
                    return new SERVER_100_PROTOCOL.MSG_SERVERINFO { RealmName = null };
                }
                return t.Result;
            })
            .PipeTo(originalSender);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_REALMLIST))]
    private void ReceiveRealmList(SERVER_100_PROTOCOL.MSG_REALMLIST message) {
        var originalSender = Sender;

        // Query every realm concurrently.
        var realmQueries = _realmToPort.Select(async kvp => {
            var (realmName, port) = kvp;
            if (!_gameServers.TryGetValue(port, out var serverRef)) {
                return null;
            }

            try {
                var msg = new SERVER_100_PROTOCOL.MSG_QUERYSERVER();
                var timeout = TimeSpan.FromSeconds(_gameServerQueryTimeout);
                var rsp = await serverRef.Ask<SERVER_100_PROTOCOL.MSG_SERVERINFO>(msg, timeout);
                return ((string realmName, ushort playerCount)?) (realmName, rsp.PlayerCount);
            }
            catch {
                // Server unreachable — skip it.
                return ((string realmName, ushort playerCount)?) null;
            }
        });

        Task.WhenAll(realmQueries)
            .ContinueWith(t => {
                var entries = t.Result.Where(r => r.HasValue).Select(r => r.Value).ToList();
                return new SERVER_100_PROTOCOL.MSG_REALMLIST_AGGREGATE {
                    OriginalSender = originalSender,
                    RealmNames = entries.Select(e => e.realmName).ToArray(),
                    PlayerCounts = entries.Select(e => e.playerCount).ToArray()
                };
            })
            .PipeTo(Self);
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_REALMLIST_AGGREGATE))]
    private void ReceiveRealmListAggregate(SERVER_100_PROTOCOL.MSG_REALMLIST_AGGREGATE result) {
        var realmNames = result.RealmNames;
        var playerCounts = result.PlayerCounts;
        var playerLimits = realmNames.Select(_ => _gameServerPlayerCount).ToArray();

        result.OriginalSender.Tell(new SERVER_100_PROTOCOL.MSG_REALMLIST {
            RealmNames = realmNames,
            PlayerCounts = playerCounts,
            PlayerLimits = playerLimits
        });
    }

}
