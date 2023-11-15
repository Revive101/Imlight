/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Models;
using Imlight.CoreLib.Game.Services;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models;
using Serilog;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandDispatcher : ReceiveProtocolDispatcher {
    private static IActorRef _instance;
    public static IActorRef Instance => _instance;

    private IActorRef _senderContext;
    private Account _accountContext;
    private Character _characterContext;
    private readonly Dictionary<string, MethodInfo> _commands = new(StringComparer.OrdinalIgnoreCase);

    public CommandDispatcher() {
        _instance = Self;

        var methods = this.GetType()
                          .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var method in methods) {
            var commandAttribute = method.GetCustomAttribute<CommandAttribute>();
            if (commandAttribute != null) {
                _commands[commandAttribute.Name.ToLower()] = method;

                var aliasAttribute = method.GetCustomAttribute<AliasAttribute>();
                foreach (var alias in aliasAttribute.Aliases) {
                    _commands[alias.ToLower()] = method;
                }
            }
        }
    }

    public static Props Props() => Akka.Actor.Props.Create(() => new CommandDispatcher());

    private void ExecuteCommand(string commandName, params object[] parameters) {
        if (_commands.TryGetValue(commandName, out var method)) {
            var authAttribute = method.GetCustomAttribute<AuthRequiredAttribute>();
            var currentAuthLevel = _accountContext.AuthLevel;
            if (authAttribute != null) {
                if (currentAuthLevel < authAttribute.Level) {
                    Logger.Warning("Command {0} requires auth level {1} but player has auth level {2}",
                                   Logger.Args(commandName, authAttribute.Level, currentAuthLevel));

                    // If the player is not capable of performing any sort of commands, log an infraction.
                    if (currentAuthLevel == AuthLevel.None) {
                        _accountContext.AddInfraction(InfractionType.SuspiciousBehavior, "Attempted to use commands without auth level");
                    }

                    return;
                }
            }

            method.Invoke(this, parameters);
        }
        else {
            Logger.Warning("Command not found: {0}", Logger.Args(commandName));
        }
    }

    [MessageHandler(typeof(SERVER_100_PROTOCOL.MSG_COMMAND))]
    private void ReceiveCommand(SERVER_100_PROTOCOL.MSG_COMMAND message) {
        Logger.Debug($"Received command: {message.CommandText}");

        // Setup context before parsing any commands.
        _senderContext = message.ActorRef;
        _characterContext = message.PlayerCharacter;
        _accountContext = message.Account;

        var parameters = message.CommandText.ToString().Split(' ');
        var command = parameters[0].ToLower();
        var arguments = parameters.Skip(1).ToArray();

        ExecuteCommand(command, arguments);
    }

    [Command("teleport")]
    [Alias("tp", "port")]
    [AuthRequired(AuthLevel.Developer)]
    private void Teleport(string zone) {
        var actualZoneName = zone;
        var hasZone = AccessPassManager.DoesZoneExist(zone);
        if (!hasZone) {
            // Fallback to the zone name that is contained in the zone name.
            actualZoneName = AccessPassManager.GetContainedZoneName(zone);

            if (!AccessPassManager.DoesZoneExist(actualZoneName)) {
                Log.Error("Teleport command was given an invalid zone name {0}", Logger.Args(zone));
                return;
            }
        }

        var msg = new ZONE_102_PROTOCOL.MSG_ZONETRANSFER() {
            DestinationZone = actualZoneName,
            DestinationLocation = "Start",
            SendToClient = true
        };
        _senderContext.Tell(msg);
    }
}
