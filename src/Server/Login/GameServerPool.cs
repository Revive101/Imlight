/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common.Configuration;
using WizUnraveler;
using Imlight.Common.Utilities;
using Imlight.Server.Shared.Networking;
using Imlight.Server.Shared.Packets;
using Imlight.Server.Game;

namespace Imlight.Server.Login;

internal class GameServerPool : ReceiveProtocolDispatcher
{
    private readonly byte _maxGameServersAllowed = ConfigurationManager.Settings.MaxGameServersAllowed;
    private readonly ushort _gameServerPlayerCount = ConfigurationManager.Settings.GameServerPlayerLimit;
        
    private readonly Dictionary<ushort, IActorRef> _gameServers;

    public GameServerPool()
    {
        this._gameServers = new Dictionary<ushort, IActorRef>();
            
        Log.Information("GameServerPool created.");
    }
        
    public static Props Props()
    {
        return Akka.Actor.Props.Create(() => new GameServerPool());
    }
        
    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER))]
    private void ReceiveCreateGameServer(SERVER_100_PROTOCOL.MSG_CREATEGAMESERVER message)
    {
        if (_gameServers.Count >= _maxGameServersAllowed)
        {
            Log.Error("{Type} attempted to create a new game server, but the " +
                      $"internal limit has already been reached. Server has not been created.", 
                Log.Args(GetType()));
            return;
        }
        if (_gameServers.Keys.Any(x => x == message.Port))
        {
            Log.Error("{Type} attempted to create a new game server, but the port" +
                      " {Port} was already in use", Log.Args(GetType(), message.Port));
            return;
        }
            
        var gameProps = GameServer.Props(message.Name, message.Port);
        var gameServerRef = Context.ActorOf(gameProps, $"{message.Name}.{message.Port}");

        _gameServers.Add(message.Port, gameServerRef);
            
        Log.Verbose("New actor created under {Path}: {Name}.{Port}",
            Log.Args(Context.Self.Path, message.Name, message.Port));
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERS))]
    private void ReceiveQueryGameServer(SERVER_100_PROTOCOL.MSG_QUERYGAMESERVERS message)
    {
        // Create a list of game servers and query each server for its details
        var gameServers = _gameServers.Values
            .Select(gameServer =>
            {
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
}